﻿using Sla.CORE;
using System.Collections.Generic;

/// Container holding the stack system for the renaming algorithm.  Every disjoint address
/// range (indexed by its initial address) maps to its own Varnode stack.
using VariableStack = System.Collections.Generic.Dictionary<Sla.CORE.Address, System.Collections.Generic.List<Sla.DECCORE.Varnode>>;

namespace Sla.DECCORE
{
    /// \brief Manage the construction of Static Single Assignment (SSA) form
    ///
    /// With a specific function (Funcdata), this class links the Varnode and
    /// PcodeOp objects into the formal data-flow graph structure, SSA form.
    /// The full structure can be built over multiple passes. In particular,
    /// this allows register data-flow to be analyzed first, and then stack
    /// locations can be discovered and promoted to first-class Varnodes in
    /// a second pass.
    ///
    /// Varnodes for which it is not known whether they are written to by a
    /// PcodeOp are referred to as \b free.  The method heritage() performs
    /// a \e single \e pass of constructing SSA form, collecting any \e eligible
    /// free Varnodes for the pass and linking them in to the data-flow. A
    /// Varnode is considered eligible for a given pass generally based on its
    /// address space (see HeritageInfo), which is the main method for delaying
    /// linking for stack locations until they are all discovered. In
    /// principle a Varnode can be discovered very late and still get linked
    /// in on a subsequent pass. Linking causes Varnodes to gain new descendant
    /// PcodeOps, which has impact on dead code elimination (see LocationMap).
    ///
    /// The two big aspects of SSA construction are phi-node placement, performed
    /// by placeMultiequals(), and the \e renaming algorithm, performed by rename().
    /// The various guard* methods are concerned with labeling analyzing
    /// data-flow across function calls, STORE, and LOAD operations.
    ///
    /// The phi-node placement algorithm is from (preprint?)
    /// "The Static Single Assignment Form and its Computation"
    /// by Gianfranco Bilardi and Keshav Pingali, July 22, 1999
    ///
    /// The renaming algorithm taken from
    /// "Efficiently computing static single assignment form and the
    ///  control dependence graph."
    /// R. Cytron, J. Ferrante, B. K. Rosen, M. N. Wegman, and F. K. Zadeck
    /// ACM Transactions on Programming Languages and Systems,
    /// 13(4):451-490, October 1991
    internal class Heritage
    {
        /// Extra boolean properties on basic blocks for the Augmented Dominator Tree
        enum heritage_flags
        {
            boundary_node = 1,      ///< Augmented Dominator Tree boundary node
            mark_node = 2,      ///< Node has already been in queue
            merged_node = 4     ///< Node has already been merged
        };

        /// \brief Node for depth-first traversal of stack references
        private struct StackNode
        {
            internal enum IndexType
            {
                nonconstant_index = 1,
                multiequal = 2
            }

            /// Varnode being traversed
            internal Varnode vn;
            /// Offset relative to base
            internal ulong offset;
            /// What kind of operations has this pointer accumulated
            internal uint traversals;
            /// Next PcodeOp to follow
            internal IEnumerator<PcodeOp> iter;

            /// \brief Constructor
            /// \param v is the Varnode being visited
            /// \param o is the current offset from the base pointer
            /// \param trav indicates what configurations were seen along the path to this Varnode
            internal StackNode(Varnode v, ulong o, uint trav)
            {
                vn = v;
                offset = o;
                iter = v.beginDescend();
                traversals = trav;
            }
        }

        /// The function \b this is controlling SSA construction 
        private Funcdata fd;
        /// Disjoint cover of every heritaged memory location
        private LocationMap globaldisjoint;
        /// Disjoint cover of memory locations currently being heritaged
        private LocationMap disjoint;
        /// Parent.child edges in dominator tree
        private List<List<FlowBlock>> domchild;
        /// Augmented edges
        private List<List<FlowBlock>> augment;
        /// Block properties for phi-node placement algorithm
        private List<heritage_flags> flags;
        /// Dominator depth of individual blocks
        private List<int> depth;
        /// Maximum depth of the dominator tree
        private int maxdepth;
        /// Current pass being executed
        private int pass;

        /// Priority queue for phi-node placement
        private PriorityQueue pq;
        /// Calculate merge points (blocks containing phi-nodes)
        private List<FlowBlock> merge;
        /// Heritage status for individual address spaces
        private List<HeritageInfo> infolist;
        /// List of LOAD operations that need to be guarded
        private List<LoadGuard> loadGuard;
        /// List of STORE operations taking an indexed pointer to the stack
        private List<LoadGuard> storeGuard;
        /// List of COPY ops generated by load guards
        private List<PcodeOp> loadCopyOps;

        /// Reset heritage status for all address spaces
        private void clearInfoList()
        {
            foreach (HeritageInfo heritage in infolist)
                heritage.reset();
        }

        /// \brief Get the heritage status for the given address space
        private HeritageInfo getInfo(AddrSpace spc) => infolist[spc.getIndex()];

        /// Clear remaining stack placeholder LOADs on any call
        /// Assuming we are just about to do heritage on an address space,
        /// clear any placeholder LOADs associated with it on CALLs.
        /// \param info is state for the specific address space
        private void clearStackPlaceholders(HeritageInfo info)
        {
            int numCalls = fd.numCalls();
            for (int i = 0; i < numCalls; ++i) {
                fd.getCallSpecs(i).abortSpacebaseRelative(fd);
            }
            info.hasCallPlaceholders = false;  // Mark that clear has taken place
        }

        /// \brief Perform one level of Varnode splitting to match a JoinRecord
        ///
        /// Split all the pieces in \b lastcombo, putting them into \b nextlev in order,
        /// to get closer to the representation described by the given JoinRecord.
        /// \b nextlev contains the two split pieces for each Varnode in \b lastcombo.
        /// If a Varnode is not split this level, an extra \b null is put into
        /// \b nextlev to maintain the 2-1 mapping.
        /// \param lastcombo is the list of Varnodes to split
        /// \param nextlev will hold the new split Varnodes in a 2-1 ratio
        /// \param joinrec is the splitting specification we are trying to match
        private void splitJoinLevel(List<Varnode> lastcombo, List<Varnode> nextlev,
            JoinRecord joinrec)
        {
            int numpieces = joinrec.numPieces();
            int recnum = 0;
            for (int i = 0; i < lastcombo.size(); ++i) {
                Varnode curvn = lastcombo[i];
                if (curvn.getSize() == joinrec.getPiece((uint)recnum).size) {
                    nextlev.Add(curvn);
                    nextlev.Add((Varnode)null);
                    recnum += 1;
                }
                else {
                    int sizeaccum = 0;
                    int j;
                    for (j = recnum; j < numpieces; ++j) {
                        sizeaccum += (int)joinrec.getPiece((uint)j).size;
                        if (sizeaccum == curvn.getSize()) {
                            j += 1;
                            break;
                        }
                    }
                    int numinhalf = (j - recnum) / 2;  // Will be at least 1
                    sizeaccum = 0;
                    for (int k = 0; k < numinhalf; ++k)
                        sizeaccum += (int)joinrec.getPiece((uint)(recnum + k)).size;
                    Varnode mosthalf, leasthalf;
                    if (numinhalf == 1)
                        mosthalf = fd.newVarnode(sizeaccum, joinrec.getPiece((uint)recnum).space,
                            joinrec.getPiece((uint)recnum).offset);
                    else
                        mosthalf = fd.newUnique(sizeaccum);
                    if ((j - recnum) == 2) {
                        VarnodeData vdata = joinrec.getPiece((uint)(recnum + 1));
                        leasthalf = fd.newVarnode((int)vdata.size, vdata.space, vdata.offset);
                    }
                    else
                        leasthalf = fd.newUnique(curvn.getSize() - sizeaccum);
                    nextlev.Add(mosthalf);
                    nextlev.Add(leasthalf);
                    recnum = j;
                }
            }
        }

        /// \brief Construct pieces for a \e join-space Varnode read by an operation.
        ///
        /// Given a splitting specification (JoinRecord) and a Varnode, build a
        /// concatenation expression (out of PIECE operations) that constructs the
        /// the Varnode out of the specified Varnode pieces.
        /// \param vn is the \e join-space Varnode to split
        /// \param joinrec is the splitting specification
        private void splitJoinRead(Varnode vn, JoinRecord joinrec)
        {
            PcodeOp op = vn.loneDescend(); // vn isFree, so loneDescend must be non-null
            bool preventConstCollapse = false;
            if (vn.isTypeLock()) {
                type_metatype meta = vn.getType().getMetatype();
                if (meta == type_metatype.TYPE_STRUCT || meta == type_metatype.TYPE_ARRAY)
                    preventConstCollapse = true;
            }

            List<Varnode> lastcombo = new List<Varnode>();
            List<Varnode> nextlev = new List<Varnode>();
            lastcombo.Add(vn);
            while (lastcombo.size() < joinrec.numPieces()) {
                nextlev.Clear();
                splitJoinLevel(lastcombo, nextlev, joinrec);

                for (int i = 0; i < lastcombo.size(); ++i) {
                    Varnode curvn = lastcombo[i];
                    Varnode mosthalf = nextlev[2 * i];
                    Varnode leasthalf = nextlev[2 * i + 1];
                    if (leasthalf == (Varnode)null) continue; // Varnode didn't get split this level
                    PcodeOp concat = fd.newOp(2, op.getAddr());
                    fd.opSetOpcode(concat, OpCode.CPUI_PIECE);
                    fd.opSetOutput(concat, curvn);
                    fd.opSetInput(concat, mosthalf, 0);
                    fd.opSetInput(concat, leasthalf, 1);
                    fd.opInsertBefore(concat, op);
                    if (preventConstCollapse)
                        fd.opMarkNoCollapse(concat);
                    mosthalf.setPrecisHi();    // Set precision flags to trigger "double precision" rules
                    leasthalf.setPrecisLo();
                    op = concat;        // Keep -op- as the earliest op in the concatenation construction
                }

                lastcombo.Clear();
                for (int i = 0; i < nextlev.size(); ++i) {
                    Varnode curvn = nextlev[i];
                    if (curvn != (Varnode)null)
                        lastcombo.Add(curvn);
                }
            }
        }

        /// \brief Split a written \e join-space Varnode into specified pieces
        ///
        /// Given a splitting specification (JoinRecord) and a Varnode, build a
        /// series of expressions that construct the specified Varnode pieces
        /// using SUBPIECE ops.
        /// \param vn is the Varnode to split
        /// \param joinrec is the splitting specification
        private void splitJoinWrite(Varnode vn, JoinRecord joinrec)
        {
            PcodeOp op = vn.getDef() ?? throw new BugException(); // vn cannot be free, either it has def, or it is input
            BlockBasic bb = (BlockBasic)fd.getBasicBlocks().getBlock(0);

            List<Varnode> lastcombo = new List<Varnode>();
            List<Varnode> nextlev = new List<Varnode>();
            lastcombo.Add(vn);
            while (lastcombo.size() < joinrec.numPieces()) {
                nextlev.Clear();
                splitJoinLevel(lastcombo, nextlev, joinrec);
                for (int i = 0; i < lastcombo.size(); ++i) {
                    Varnode curvn = lastcombo[i];
                    Varnode mosthalf = nextlev[2 * i];
                    Varnode leasthalf = nextlev[2 * i + 1];
                    if (leasthalf == (Varnode)null) continue; // Varnode didn't get split this level
                    PcodeOp split;
                    if (vn.isInput())
                        split = fd.newOp(2, bb.getStart());
                    else
                        split = fd.newOp(2, op.getAddr());
                    fd.opSetOpcode(split, OpCode.CPUI_SUBPIECE);
                    fd.opSetOutput(split, mosthalf);
                    fd.opSetInput(split, curvn, 0);
                    fd.opSetInput(split, fd.newConstant(4, (ulong)leasthalf.getSize()), 1);
                    if (op == (PcodeOp)null)
                        fd.opInsertBegin(split, bb);
                    else
                        fd.opInsertAfter(split, op);
                    op = split;     // Keep -op- as the latest op in the split construction

                    split = fd.newOp(2, op.getAddr());
                    fd.opSetOpcode(split, OpCode.CPUI_SUBPIECE);
                    fd.opSetOutput(split, leasthalf);
                    fd.opSetInput(split, curvn, 0);
                    fd.opSetInput(split, fd.newConstant(4, 0), 1);
                    fd.opInsertAfter(split, op);
                    mosthalf.setPrecisHi();    // Make sure we set the precision flags to trigger "double precision" rules
                    leasthalf.setPrecisLo();
                    op = split;     // Keep -op- as the latest op in the split construction
                }

                lastcombo.Clear();
                for (int i = 0; i < nextlev.size(); ++i) {
                    Varnode curvn = nextlev[i];
                    if (curvn != (Varnode)null)
                        lastcombo.Add(curvn);
                }
            }
        }

        /// \brief Create float truncation into a free lower precision \e join-space Varnode
        ///
        /// Given a Varnode with logically lower precision, as given by a
        /// float extension record (JoinRecord), create the real full-precision Varnode
        /// and define the lower precision Varnode as a truncation (FLOAT2FLOAT)
        /// \param vn is the lower precision \e join-space input Varnode
        /// \param joinrec is the float extension record
        private void floatExtensionRead(Varnode vn, JoinRecord joinrec)
        {
            PcodeOp op = vn.loneDescend() ?? throw new BugException(); // vn isFree, so loneDescend must be non-null
            PcodeOp trunc = fd.newOp(1, op.getAddr());
            VarnodeData vdata = joinrec.getPiece(0); // Float extensions have exactly 1 piece
            Varnode bigvn = fd.newVarnode((int)vdata.size, vdata.space, vdata.offset);
            fd.opSetOpcode(trunc, OpCode.CPUI_FLOAT_FLOAT2FLOAT);
            fd.opSetOutput(trunc, vn);
            fd.opSetInput(trunc, bigvn, 0);
            fd.opInsertBefore(trunc, op);
        }

        /// \brief Create float extension from a lower precision \e join-space Varnode
        ///
        /// Given a Varnode with logically lower precision, as given by a
        /// float extension record (JoinRecord), create the full precision Varnode
        /// specified by the record, making it defined by an extension (FLOAT2FLOAT).
        /// \param vn is the lower precision \e join-space output Varnode
        /// \param joinrec is the float extension record
        private void floatExtensionWrite(Varnode vn, JoinRecord joinrec)
        {
            PcodeOp op = vn.getDef() ?? throw new BugException();
            BlockBasic bb = (BlockBasic)fd.getBasicBlocks().getBlock(0);
            PcodeOp ext;
            if (vn.isInput())
                ext = fd.newOp(1, bb.getStart());
            else
                ext = fd.newOp(1, op.getAddr());
            VarnodeData vdata = joinrec.getPiece(0); // Float extensions have exactly 1 piece
            fd.opSetOpcode(ext, OpCode.CPUI_FLOAT_FLOAT2FLOAT);
            fd.newVarnodeOut((int)vdata.size, vdata.getAddr(), ext);
            fd.opSetInput(ext, vn, 0);
            if (op == (PcodeOp)null)
                fd.opInsertBegin(ext, bb);
            else
                fd.opInsertAfter(ext, op);
        }

        /// \brief Split \e join-space Varnodes up into their real components
        ///
        /// For any Varnode in the \e join-space, look up its JoinRecord and
        /// split it up into the specified real components so that
        /// join-space addresses play no role in the heritage process,
        /// i.e. there should be no free Varnodes in the \e join-space.
        private void processJoins()
        {
            AddrSpace joinspace = fd.getArch().getJoinSpace();
            IEnumerator<Varnode> iter, enditer;

            iter = fd.beginLoc(joinspace);
            enditer = fd.endLoc(joinspace);

            while (iter != enditer) {
                Varnode vn = *iter++;
                if (vn.getSpace() != joinspace) break; // New varnodes may get inserted before enditer
                JoinRecord joinrec = fd.getArch().findJoin(vn.getOffset());
                AddrSpace piecespace = joinrec.getPiece(0).space;

                if (joinrec.getUnified().size != vn.getSize())
                    throw new LowlevelError("Joined varnode does not match size of record");
                if (vn.isFree()) {
                    if (joinrec.isFloatExtension())
                        floatExtensionRead(vn, joinrec);
                    else
                        splitJoinRead(vn, joinrec);
                }

                HeritageInfo info = getInfo(piecespace);
                if (pass != info.delay) continue; // It is too soon to heritage this space

                if (joinrec.isFloatExtension())
                    floatExtensionWrite(vn, joinrec);
                else
                    splitJoinWrite(vn, joinrec);    // Only do this once for a particular varnode
            }
        }

        /// Build the augmented dominator tree
        /// Assume the dominator tree is already built. Assume nodes are in dfs order.
        private void buildADT()
        {
            BlockGraph bblocks = fd.getBasicBlocks();
            int size = bblocks.getSize();
            List<int> a = new List<int>(size);
            List<int> b = new List<int>(size);
            List<int> t = new List<int>(size);
            List<int> z = new List<int>(size);
            List<FlowBlock> upstart = new List<FlowBlock>();
            List<FlowBlock> upend = new List<FlowBlock>();  // Up edges (node pair)
            FlowBlock x;
            FlowBlock u;
            FlowBlock v;
            int i, j, k, l;

            augment.Clear();
            augment.resize(size);
            flags.Clear();
            // flags.resize(size, 0);

            bblocks.buildDomTree(domchild);
#if DFSVERIFY_DEBUG
            verify_dfs(bblocks.getList(), domchild);
#endif
            maxdepth = bblocks.buildDomDepth(depth);
            for (i = 0; i < size; ++i) {
                x = bblocks.getBlock(i);
                for (j = 0; j < domchild[i].size(); ++j) {
                    v = domchild[i][j];
                    for (k = 0; k < v.sizeIn(); ++k) {
                        u = v.getIn(k);
                        if (u != v.getImmedDom()) {
                            // If u.v is an up-edge
                            upstart.Add(u);   // Store edge (in dfs order)
                            upend.Add(v);
                            b[u.getIndex()] += 1;
                            t[x.getIndex()] += 1;
                        }
                    }
                }
            }
            for (i = size - 1; i >= 0; --i) {
                k = 0;
                l = 0;
                for (j = 0; j < domchild[i].size(); ++j) {
                    k += a[domchild[i][j].getIndex()];
                    l += z[domchild[i][j].getIndex()];
                }
                a[i] = b[i] - t[i] + k;
                z[i] = 1 + l;
                if ((domchild[i].size() == 0) || (z[i] > a[i] + 1)) {
                    flags[i] |= heritage_flags.boundary_node; // Mark this node as a boundary node
                    z[i] = 1;
                }
            }
            z[0] = -1;
            for (i = 1; i < size; ++i) {
                j = bblocks.getBlock(i).getImmedDom().getIndex();
                if ((flags[j] & heritage_flags.boundary_node) != 0) // If j is a boundary node
                    z[i] = j;
                else
                    z[i] = z[j];
            }
            for (i = 0; i < upstart.size(); ++i) {
                v = upend[i];
                j = v.getImmedDom().getIndex();
                k = upstart[i].getIndex();
                while (j < k) {
                    // while idom(v) properly dominates u
                    augment[k].Add(v);
                    k = z[k];
                }
            }
        }

        /// \brief Remove deprecated OpCode.CPUI_MULTIEQUAL or OpCode.CPUI_INDIRECT ops, preparing to re-heritage
        ///
        /// If a previous Varnode was heritaged through a MULTIEQUAL or INDIRECT op, but now
        /// a larger range containing the Varnode is being heritaged, we throw away the op,
        /// letting the data-flow for the new larger range determine the data-flow for the
        /// old Varnode.  The original Varnode is redefined as the output of a SUBPIECE
        /// of a larger free Varnode.
        /// \param remove is the list of Varnodes written by MULTIEQUAL or INDIRECT
        /// \param addr is the start of the larger range
        /// \param size is the size of the range
        private void removeRevisitedMarkers(List<Varnode> remove, Address addr, int size)
        {
            HeritageInfo info = getInfo(addr.getSpace());
            if (info.deadremoved > 0) {
                bumpDeadcodeDelay(addr.getSpace());
                if (!info.warningissued) {
                    info.warningissued = true;
                    StringWriter errmsg = new StringWriter();
                    errmsg.Write("Heritage AFTER dead removal. Revisit: ");
                    addr.printRaw(errmsg);
                    fd.warningHeader(errmsg.ToString());
                }
            }

            List<Varnode> newInputs = new List<Varnode>();
            LinkedListNode<PcodeOp>? pos;
            for (int i = 0; i < remove.size(); ++i) {
                Varnode vn = remove[i];
                PcodeOp op = vn.getDef() ?? throw new BugException();
                BlockBasic bl = op.getParent();
                if (op.code() == OpCode.CPUI_INDIRECT) {
                    Varnode iopVn = op.getIn(1);
                    PcodeOp targetOp = PcodeOp.getOpFromConst(iopVn.getAddr());
                    pos = (targetOp.isDead() ? op.getBasicIter() : targetOp.getBasicIter())
                        ?? throw new ApplicationException();
                    // Insert SUBPIECE after target of INDIRECT
                    pos = pos.Next;
                }
                else {
                    // Insert SUBPIECE after all MULTIEQUALs in block
                    pos = op.getBasicIter();
                    pos = pos.Next ?? throw new ApplicationException();
                    while ((null != (pos = pos.Next)) && pos.Value.code() == OpCode.CPUI_MULTIEQUAL) { }
                }
                int offset = vn.overlap(addr, size);
                fd.opUninsert(op);
                newInputs.Clear();
                Varnode big = fd.newVarnode(size, addr);
                big.setActiveHeritage();
                newInputs.Add(big);
                newInputs.Add(fd.newConstant(4, (ulong)offset));
                fd.opSetOpcode(op, OpCode.CPUI_SUBPIECE);
                fd.opSetAllInput(op, newInputs);
                fd.opInsert(op, bl, pos);
                vn.setWriteMask();
            }
        }

        /// \brief Collect free reads, writes, and inputs in the given address range
        ///
        /// \param addr is the starting address of the range
        /// \param size is the number of bytes in the range
        /// \param read will hold any read Varnodes in the range
        /// \param write will hold any written Varnodes
        /// \param input will hold any input Varnodes
        /// \param remove will hold any PcodeOps that need to be removed
        /// \return the maximum size of a write
        private int collect(Address addr, int size, List<Varnode> read, List<Varnode> write,
            List<Varnode> input, List<Varnode> remove)
        {
            Varnode vn;
            IEnumerator<Varnode> viter = fd.beginLoc(addr);
            IEnumerator<Varnode> enditer;
            ulong start = addr.getOffset();
            addr = addr + size;
            if (addr.getOffset() < start) {
                // Wraparound
                Address tmp = new Address(addr.getSpace(), addr.getSpace().getHighest());
                enditer = fd.endLoc(tmp);
            }
            else
                enditer = fd.beginLoc(addr);
            int maxsize = 0;
            while (viter != enditer) {
                vn = *viter;
                if (!vn.isWriteMask()) {
                    if (vn.isWritten()) {
                        if (vn.getSize() < size && vn.getDef().isMarker())
                            remove.Add(vn);
                        else {
                            if (vn.getSize() > maxsize) // Look for maximum write size
                                maxsize = vn.getSize();
                            write.Add(vn);
                        }
                    }
                    else if ((!vn.isHeritageKnown()) && (!vn.hasNoDescend()))
                        read.Add(vn);
                    else if (vn.isInput())
                        input.Add(vn);
                }
                ++viter;
            }
            return maxsize;
        }

        /// \brief Determine if the address range is affected by the given call p-code op
        ///
        /// We assume the op is CALL, CALLIND, CALLOTHER, or NEW and that its
        /// output overlaps the given address range. We look up any effect
        /// the op might have on the address range.
        /// \param addr is the starting address of the range
        /// \param size is the number of bytes in the range
        /// \param op is the given \e call p-code op
        /// \return \b true, unless the range is unaffected by the op
        private bool callOpIndirectEffect(Address addr, int size, PcodeOp op)
        {
            if ((op.code() == OpCode.CPUI_CALL) || (op.code() == OpCode.CPUI_CALLIND)) {
                // We should be able to get the callspec
                FuncCallSpecs fc = fd.getCallSpecs(op);
                if (fc == (FuncCallSpecs)null) return true;       // Assume indirect effect
                return (fc.hasEffectTranslate(addr, size) != EffectRecord.EffectType.unaffected);
            }
            // If we reach here, this is a CALLOTHER, NEW
            // We assume these do not have effects on -fd- variables except for op.getOut().
            return false;
        }

        /// \brief Normalize the size of a read Varnode, prior to heritage
        ///
        /// Given a Varnode being read that does not match the (larger) size
        /// of the address range currently being linked, create a Varnode of
        /// the correct size and define the original Varnode as a SUBPIECE.
        /// \param vn is the given too small Varnode
        /// \param addr is the start of the (larger) range
        /// \param size is the number of bytes in the range
        /// \return the new larger Varnode
        private Varnode normalizeReadSize(Varnode vn, Address addr, int size)
        {
            int overlap;
            Varnode vn1, vn2;
            PcodeOp op, newop;

            IEnumerator<PcodeOp> oiter = vn.beginDescend();
            if (!oiter.MoveNext()) {
                throw new BugException();
            }
            op = oiter.Current;
            if (oiter.MoveNext())
                throw new LowlevelError("Free varnode with multiple reads");
            newop = fd.newOp(2, op.getAddr());
            fd.opSetOpcode(newop, OpCode.CPUI_SUBPIECE);
            vn1 = fd.newVarnode(size, addr);
            overlap = vn.overlap(addr, size);
            vn2 = fd.newConstant(addr.getAddrSize(), (ulong)overlap);
            fd.opSetInput(newop, vn1, 0);
            fd.opSetInput(newop, vn2, 1);
            fd.opSetOutput(newop, vn); // Old vn is no longer a free read
            newop.getOut().setWriteMask();
            fd.opInsertBefore(newop, op);
            return vn1;         // But we have new free read of uniform size
        }

        /// \brief Normalize the size of a written Varnode, prior to heritage
        ///
        /// Given a Varnode that is written that does not match the (larger) size
        /// of the address range currently being linked, create the missing
        /// pieces in the range and concatenate everything into a new Varnode
        /// of the correct size.
        ///
        /// One or more Varnode pieces are created depending
        /// on how the original Varnode overlaps the given range. An expression
        /// is created using PIECE ops resulting in a final Varnode.
        /// \param vn is the given too small Varnode
        /// \param addr is the start of the (larger) range
        /// \param size is the number of bytes in the range
        /// \return the newly created final Varnode
        private Varnode normalizeWriteSize(Varnode vn, Address addr, int size)
        {
            int overlap;
            int mostsigsize;
            PcodeOp op;
            PcodeOp newop;
            Varnode mostvn;
            Varnode leastvn;
            Varnode big;
            Varnode bigout;
            Varnode midvn;

            mostvn = (Varnode)null;
            op = vn.getDef() ?? throw new BugException();
            overlap = vn.overlap(addr, size);
            mostsigsize = size - (overlap + vn.getSize());
            if (mostsigsize != 0) {
                Address pieceaddr;
                if (addr.isBigEndian())
                    pieceaddr = addr;
                else
                    pieceaddr = addr + (overlap + vn.getSize());
                if (op.isCall() && callOpIndirectEffect(pieceaddr, mostsigsize, op)) {
                    // Does CALL have an effect on piece
                    newop = fd.newIndirectCreation(op, pieceaddr, mostsigsize, false); // Don't create a new big read if write is from a CALL
                    mostvn = newop.getOut();
                }
                else {
                    newop = fd.newOp(2, op.getAddr());
                    mostvn = fd.newVarnodeOut(mostsigsize, pieceaddr, newop);
                    big = fd.newVarnode(size, addr);   // The new read
                    big.setActiveHeritage();
                    fd.opSetOpcode(newop, OpCode.CPUI_SUBPIECE);
                    fd.opSetInput(newop, big, 0);
                    fd.opSetInput(newop, fd.newConstant(addr.getAddrSize(), (ulong)(overlap + vn.getSize())), 1);
                    fd.opInsertBefore(newop, op);
                }
            }
            if (overlap != 0) {
                Address pieceaddr = addr.isBigEndian() ? addr + (size - overlap) : addr;
                if (op.isCall() && callOpIndirectEffect(pieceaddr, overlap, op)) {
                    // Unless CALL definitely has no effect on piece
                    // Don't create a new big read if write is from a CALL
                    newop = fd.newIndirectCreation(op, pieceaddr, overlap, false);
                    leastvn = newop.getOut();
                }
                else {
                    newop = fd.newOp(2, op.getAddr());
                    leastvn = fd.newVarnodeOut(overlap, pieceaddr, newop);
                    big = fd.newVarnode(size, addr);   // The new read
                    big.setActiveHeritage();
                    fd.opSetOpcode(newop, OpCode.CPUI_SUBPIECE);
                    fd.opSetInput(newop, big, 0);
                    fd.opSetInput(newop, fd.newConstant(addr.getAddrSize(), 0), 1);
                    fd.opInsertBefore(newop, op);
                }
                newop = fd.newOp(2, op.getAddr());
                if (addr.isBigEndian())
                    midvn = fd.newVarnodeOut(overlap + vn.getSize(), vn.getAddr(), newop);
                else
                    midvn = fd.newVarnodeOut(overlap + vn.getSize(), addr, newop);
                fd.opSetOpcode(newop, OpCode.CPUI_PIECE);
                fd.opSetInput(newop, vn, 0); // Most significant part
                fd.opSetInput(newop, leastvn, 1); // Least sig
                fd.opInsertAfter(newop, op);
            }
            else
                midvn = vn;
            if (mostsigsize != 0)
            {
                newop = fd.newOp(2, op.getAddr());
                bigout = fd.newVarnodeOut(size, addr, newop);
                fd.opSetOpcode(newop, OpCode.CPUI_PIECE);
                fd.opSetInput(newop, mostvn, 0);
                fd.opSetInput(newop, midvn, 1);
                fd.opInsertAfter(newop, midvn.getDef());
            }
            else
                bigout = midvn;
            vn.setWriteMask();
            return bigout;      // Replace small write with big write
        }

        /// \brief Concatenate a list of Varnodes together at the given location
        ///
        /// There must be at least 2 Varnodes in list, they must be in order
        /// from most to least significant.  The Varnodes in the list become
        /// inputs to a single expression of PIECE ops resulting in a
        /// final specified Varnode
        /// \param vnlist is the list of Varnodes to concatenate
        /// \param insertop is the point where the expression should be inserted (before)
        /// \param finalvn is the final specified output Varnode of the expression
        /// \return the final unified Varnode
        private Varnode concatPieces(List<Varnode> vnlist, PcodeOp? insertop, Varnode finalvn)
        {
            Varnode preexist = vnlist[0];
            bool isbigendian = preexist.getAddr().isBigEndian();
            Address opaddress;
            BlockBasic bl;
            LinkedListNode<PcodeOp> insertiter;

            if (insertop == (PcodeOp)null) {
                // Insert at the beginning
                bl = (BlockBasic)fd.getBasicBlocks().getStartBlock();
                insertiter = bl.beginOp() ?? throw new ApplicationException();
                opaddress = fd.getAddress();
            }
            else {
                bl = insertop.getParent();
                insertiter = insertop.getBasicIter();
                opaddress = insertop.getAddr();
            }

            for (int i = 1; i < vnlist.size(); ++i) {
                Varnode vn = vnlist[i];
                PcodeOp newop = fd.newOp(2, opaddress);
                fd.opSetOpcode(newop, OpCode.CPUI_PIECE);
                Varnode newvn;
                if (i == vnlist.size() - 1) {
                    newvn = finalvn;
                    fd.opSetOutput(newop, newvn);
                }
                else
                    newvn = fd.newUniqueOut(preexist.getSize() + vn.getSize(), newop);
                if (isbigendian) {
                    fd.opSetInput(newop, preexist, 0); // Most sig part
                    fd.opSetInput(newop, vn, 1); // Least sig part
                }
                else {
                    fd.opSetInput(newop, vn, 0);
                    fd.opSetInput(newop, preexist, 1);
                }
                fd.opInsert(newop, bl, insertiter);
                preexist = newvn;
            }
            return preexist;
        }

        /// \brief Build a set of Varnode piece expression at the given location
        ///
        /// Given a list of small Varnodes and the address range they are a piece of,
        /// construct a SUBPIECE op that defines each piece.  The truncation parameters
        /// are calculated based on the overlap of the piece with the whole range,
        /// and a single input Varnode is used for all SUBPIECE ops.
        /// \param vnlist is the list of piece Varnodes
        /// \param insertop is the point where the op expressions are inserted (before)
        /// \param addr is the first address of the whole range
        /// \param size is the number of bytes in the whole range
        /// \param startvn is designated input Varnode
        private void splitPieces(List<Varnode> vnlist, PcodeOp insertop, Address addr, int size,
            Varnode startvn)
        {
            Address opaddress;
            ulong baseoff;
            bool isbigendian;
            BlockBasic bl;
            LinkedListNode<PcodeOp>? insertiter;

            isbigendian = addr.isBigEndian();
            baseoff = (isbigendian) ? addr.getOffset() + (uint)size : addr.getOffset();
            if (insertop == (PcodeOp)null) {
                bl = (BlockBasic)fd.getBasicBlocks().getStartBlock();
                insertiter = bl.beginOp() ?? throw new ApplicationException();
                opaddress = fd.getAddress();
            }
            else {
                bl = insertop.getParent();
                insertiter = insertop.getBasicIter() ?? throw new ApplicationException();
                // Insert AFTER the write
                insertiter = insertiter.Next;
                opaddress = insertop.getAddr();
            }

            for (int i = 0; i < vnlist.size(); ++i) {
                Varnode vn = vnlist[i];
                PcodeOp newop = fd.newOp(2, opaddress);
                fd.opSetOpcode(newop, OpCode.CPUI_SUBPIECE);
                ulong diff = isbigendian
                    ? baseoff - (vn.getOffset() + (uint)vn.getSize())
                    : vn.getOffset() - baseoff;
                fd.opSetInput(newop, startvn, 0);
                fd.opSetInput(newop, fd.newConstant(4, diff), 1);
                fd.opSetOutput(newop, vn);
                fd.opInsert(newop, bl, insertiter);
            }
        }

        /// \brief Find the last PcodeOps that write to specific addresses that flow to specific sites
        ///
        /// Given a set of sites for which data-flow needs to be preserved at a specific address, find
        /// the \e last ops that write to the address such that data flows to the site
        /// only through \e artificial COPYs and MULTIEQUALs.  A COPY/MULTIEQUAL is artificial if all
        /// of its input and output Varnodes have the same storage address.  The specific sites are
        /// presented as artificial COPY ops.  The final set of ops that are not artificial will all
        /// have an output Varnode that matches the specific address of a COPY sink and will need to
        /// be marked address forcing. The original set of COPY sinks will be extended to all artificial
        /// COPY/MULTIEQUALs encountered.  Every PcodeOp encountered will have its mark set.
        /// \param copySinks is the list of sinks that we are trying to find flow to
        /// \param forces is the final list of address forcing PcodeOps
        private void findAddressForces(List<PcodeOp> copySinks, List<PcodeOp> forces)
        {
            // Mark the sinks
            for (int i = 0; i < copySinks.size(); ++i) {
                PcodeOp op = copySinks[i];
                op.setMark();
            }

            // Mark everything back-reachable from a sink, trimming at non-artificial ops
            int pos = 0;
            while (pos < copySinks.size()) {
                PcodeOp op = copySinks[pos];
                // Address being flowed to
                Address addr = op.getOut().getAddr();
                pos += 1;
                int maxIn = op.numInput();
                for (int i = 0; i < maxIn; ++i) {
                    Varnode vn = op.getIn(i);
                    if (!vn.isWritten()) {
                        continue;
                    }
                    if (vn.isAddrForce()) {
                        // Already marked address forced
                        continue;
                    }
                    PcodeOp newOp = vn.getDef() ?? throw new BugException();
                    if (newOp.isMark()) {
                        // Already visited this op
                        continue;
                    }
                    newOp.setMark();
                    OpCode opc = newOp.code();
                    bool isArtificial = false;
                    if (opc == OpCode.CPUI_COPY || opc == OpCode.CPUI_MULTIEQUAL) {
                        isArtificial = true;
                        int maxInNew = newOp.numInput();
                        for (int j = 0; j < maxInNew; ++j) {
                            Varnode inVn = newOp.getIn(j);
                            if (addr != inVn.getAddr()) {
                                isArtificial = false;
                                break;
                            }
                        }
                    }
                    else if (opc == OpCode.CPUI_INDIRECT && newOp.isIndirectStore()) {
                        // An INDIRECT can be considered artificial if it is caused by a STORE
                        Varnode inVn = newOp.getIn(0);
                        if (addr == inVn.getAddr()) {
                            isArtificial = true;
                        }
                    }
                    if (isArtificial) {
                        copySinks.Add(newOp);
                    }
                    else {
                        forces.Add(newOp);
                    }
                }
            }
        }

        /// \brief Eliminate a COPY sink preserving its data-flow
        ///
        /// Given a COPY from a storage location to itself, propagate the input Varnode
        /// version of the storage location to all the ops reading the output Varnode, so
        /// the output no longer has any descendants. Then eliminate the COPY.
        /// \param op is the given COPY sink
        private void propagateCopyAway(PcodeOp op)
        {
            Varnode inVn = op.getIn(0);
            while (inVn.isWritten()) {
                // Follow any COPY chain to earliest input
                PcodeOp nextOp = inVn.getDef() ?? throw new BugException();
                if (nextOp.code() != OpCode.CPUI_COPY)
                    break;
                Varnode nextIn = nextOp.getIn(0);
                if (nextIn.getAddr() != inVn.getAddr())
                    break;
                inVn = nextIn;
            }
            fd.totalReplace(op.getOut(), inVn);
            fd.opDestroy(op);
        }

        /// \brief Mark the boundary of artificial ops introduced by load guards
        ///
        /// Having just completed renaming, run through all new COPY sinks from load guards
        /// and mark boundary Varnodes (Varnodes whose data-flow along all paths traverses only
        /// COPY/INDIRECT/MULTIEQUAL ops and hits a load guard). This lets dead code removal
        /// run forward from the boundary while still preserving the address force on the load guard.
        private void handleNewLoadCopies()
        {
            if (loadCopyOps.empty()) return;
            List<PcodeOp> forces = new List<PcodeOp>();
            int copySinkSize = loadCopyOps.size();
            findAddressForces(loadCopyOps, forces);

            if (!forces.empty()) {
                RangeList loadRanges = new RangeList();
                foreach (LoadGuard guard in loadGuard) {
                    loadRanges.insertRange(guard.spc, guard.minimumOffset, guard.maximumOffset);
                }
                // Mark everything on the boundary as address forced to prevent dead-code removal
                for (int i = 0; i < forces.size(); ++i) {
                    PcodeOp op = forces[i];
                    Varnode vn = op.getOut();
                    if (loadRanges.inRange(vn.getAddr(), 1)) {
                        // If we are within one of the guarded ranges
                        // then consider the output address forced
                        vn.setAddrForce();
                    }
                    op.clearMark();
                }
            }

            // Eliminate or propagate away original COPY sinks
            for (int i = 0; i < copySinkSize; ++i) {
                PcodeOp op = loadCopyOps[i];
                // Make sure load guard COPYs no longer exist
                propagateCopyAway(op);
            }
            // Clear marks on remaining artificial COPYs
            for (int i = copySinkSize; i < loadCopyOps.size(); ++i) {
                PcodeOp op = loadCopyOps[i];
                op.clearMark();
            }
            // We have handled all the load guard COPY ops
            loadCopyOps.Clear();
        }

        /// \brief Make final determination of what range new LoadGuards are protecting
        ///
        /// Actual LOAD operations are guarded with an initial version of the LoadGuard record.
        /// Now that heritage has completed, a full analysis of each LOAD is conducted, using
        /// value set analysis, to reach a conclusion about what range of stack values the
        /// LOAD might actually alias.  All new LoadGuard records are updated with the analysis,
        /// which then informs handling of LOAD COPYs and possible later heritage passes.
        private void analyzeNewLoadGuards()
        {
            bool nothingToDo = true;
            if (!loadGuard.empty()) {
                if (loadGuard.GetLastItem().analysisState == 0) {
                    // Check if unanalyzed
                    nothingToDo = false;
                }
            }
            if (!storeGuard.empty()) {
                if (storeGuard.GetLastItem().analysisState == 0) {
                    nothingToDo = false;
                }
            }
            if (nothingToDo) return;

            List<Varnode> sinks = new List<Varnode>();
            List<PcodeOp> reads = new List<PcodeOp>();
            for (int index = loadGuard.Count - 1; 0 <= index; index--) {
                LoadGuard guard = loadGuard[index];
                if (guard.analysisState != 0) break;
                reads.Add(guard.op);
                // The OpCode.CPUI_LOAD pointer
                sinks.Add(guard.op.getIn(1));
            }
            for (int index = storeGuard.Count - 1; 0 <= index; index--) {
                LoadGuard guard = storeGuard[index];
                if (guard.analysisState != 0) break;
                reads.Add(guard.op);
                // The OpCode.CPUI_STORE pointer
                sinks.Add(guard.op.getIn(1));
            }
            AddrSpace stackSpc = fd.getArch().getStackSpace();
            Varnode? stackReg = (Varnode)null;
            if (stackSpc != (AddrSpace)null && stackSpc.numSpacebase() > 0)
                stackReg = fd.findSpacebaseInput(stackSpc);
            ValueSetSolver vsSolver = new ValueSetSolver();
            vsSolver.establishValueSets(sinks, reads, stackReg, false);
            WidenerNone widener = new WidenerNone();
            vsSolver.solve(10000, widener);
            bool runFullAnalysis = false;
            IEnumerator<LoadGuard> loadIter = loadGuard.GetEnumerator();
            while (loadIter.MoveNext()) {
                LoadGuard guard = loadIter.Current;
                guard.establishRange(vsSolver.getValueSetRead(guard.op.getSeqNum()));
                if (guard.analysisState == 0) {
                    runFullAnalysis = true;
                }
            }
            IEnumerator<LoadGuard> iter = storeGuard.GetEnumerator();
            while (iter.MoveNext()) {
                LoadGuard guard = iter.Current;
                guard.establishRange(vsSolver.getValueSetRead(guard.op.getSeqNum()));
                if (guard.analysisState == 0)
                    runFullAnalysis = true;
            }
            if (runFullAnalysis) {
                WidenerFull fullWidener = new WidenerFull();
                vsSolver.solve(10000, fullWidener);
                iter = loadGuard.GetEnumerator();
                while (iter.MoveNext()) {
                    LoadGuard guard = iter.Current;
                    guard.finalizeRange(vsSolver.getValueSetRead(guard.op.getSeqNum()));
                }
                iter = storeGuard.GetEnumerator();
                while (iter.MoveNext()) {
                    LoadGuard guard = iter.Current;
                    guard.finalizeRange(vsSolver.getValueSetRead(guard.op.getSeqNum()));
                }
            }
        }

        /// \brief Generate a guard record given an indexed LOAD into a stack space
        ///
        /// Record the LOAD op and the (likely) range of addresses in the stack space that
        /// might be loaded from.
        /// \param node is the path element containing the constructed Address
        /// \param op is the LOAD PcodeOp
        /// \param spc is the stack space
        private void generateLoadGuard(StackNode node, PcodeOp op, AddrSpace spc)
        {
            if (!op.usesSpacebasePtr()) {
                LoadGuard newGuard = new LoadGuard();
                newGuard.set(op, spc, node.offset);
                loadGuard.Add(newGuard);
                fd.opMarkSpacebasePtr(op);
            }
        }

        /// \brief Generate a guard record given an indexed STORE to a stack space
        ///
        /// Record the STORE op and the (likely) range of addresses in the stack space that
        /// might be stored to.
        /// \param node is the path element containing the constructed Address
        /// \param op is the STORE PcodeOp
        /// \param spc is the stack space
        private void generateStoreGuard(StackNode node, PcodeOp op, AddrSpace spc)
        {
            if (!op.usesSpacebasePtr()) {
                LoadGuard newGuard = new LoadGuard();
                newGuard.set(op, spc, node.offset);
                storeGuard.Add(newGuard);
                fd.opMarkSpacebasePtr(op);
            }
        }

        /// \brief Identify any OpCode.CPUI_STORE ops that use a free pointer from a given address space
        ///
        /// When performing heritage for stack Varnodes, data-flow around a STORE with a
        /// free pointer must be guarded (with an INDIRECT) to be safe. This routine collects
        /// and marks the STORE ops that trigger this guard.
        /// \param spc is the given address space
        /// \param freeStores will hold the list of STOREs if any
        /// \return \b true if there are any new STOREs needing a guard
        private bool protectFreeStores(AddrSpace spc, List<PcodeOp> freeStores)
        {
            IEnumerator<PcodeOp> iter = fd.beginOp(OpCode.CPUI_STORE);
            bool hasNew = false;
            while (iter.MoveNext()) {
                PcodeOp op = iter.Current;
                if (op.isDead()) continue;
                Varnode vn = op.getIn(1);
                while (vn.isWritten()) {
                    PcodeOp defOp = vn.getDef() ?? throw new BugException();
                    OpCode opc = defOp.code();
                    if (opc == OpCode.CPUI_COPY)
                        vn = defOp.getIn(0);
                    else if (opc == OpCode.CPUI_INT_ADD && defOp.getIn(1).isConstant())
                        vn = defOp.getIn(0);
                    else
                        break;
                }
                if (vn.isFree() && vn.getSpace() == spc) {
                    fd.opMarkSpacebasePtr(op); // Mark op as spacebase STORE, even though we're not sure
                    freeStores.Add(op);
                    hasNew = true;
                }
            }
            return hasNew;
        }

        /// \brief Trace input stack-pointer to any indexed loads
        ///
        /// Look for expressions of the form  val = *(SP(i) + vn + \#c), where the base stack
        /// pointer has an (optional) constant added to it and a non-constant index, then a
        /// value is loaded from the resulting address.  The LOAD operations are added to the list
        /// of ops that potentially need to be guarded during a heritage pass.  The routine can
        /// checks for STOREs where the data-flow path hasn't been completed yet and returns
        /// \b true if they exist, passing back a list of those that might use a pointer to the stack.
        /// \param spc is the particular address space with a stackpointer (into it)
        /// \param freeStores will hold the list of any STOREs that need follow-up analysis
        /// \param checkFreeStores is \b true if the routine should check for free STOREs
        /// \return \b true if there are incomplete STOREs
        private bool discoverIndexedStackPointers(AddrSpace spc, List<PcodeOp> freeStores,
            bool checkFreeStores)
        {
            // We need to be careful of exponential ladders, so we mark Varnodes independently of
            // the depth first path we are traversing.
            List<Varnode> markedVn = new List<Varnode>();
            List<StackNode> path = new List<StackNode>();
            bool unknownStackStorage = false;
            for (int i = 0; i < spc.numSpacebase(); ++i) {
                VarnodeData stackPointer = spc.getSpacebase(i);
                Varnode? spInput = fd.findVarnodeInput((int)stackPointer.size, stackPointer.getAddr());
                if (spInput == (Varnode)null) continue;
                path.Add(new StackNode(spInput, 0, 0));
                while (!path.empty()) {
                    StackNode curNode = path.GetLastItem();
                    if (curNode.iter == curNode.vn.endDescend()) {
                        path.RemoveLastItem();
                        continue;
                    }
                    PcodeOp op = curNode.iter;
                    ++curNode.iter;
                    Varnode? outVn = op.getOut();
                    if (outVn != (Varnode)null && outVn.isMark()) continue;      // Don't revisit Varnodes
                    switch (op.code()) {
                        case OpCode.CPUI_INT_ADD:
                            {
                                Varnode otherVn = op.getIn(1 - op.getSlot(curNode.vn));
                                if (otherVn.isConstant()) {
                                    ulong newOffset = spc.wrapOffset(curNode.offset + otherVn.getOffset());
                                    StackNode nextNode = new StackNode(outVn, newOffset, curNode.traversals);
                                    if (nextNode.iter != nextNode.vn.endDescend()) {
                                        outVn.setMark();
                                        path.Add(nextNode);
                                        markedVn.Add(outVn);
                                    }
                                    else if (outVn.getSpace().getType() == spacetype.IPTR_SPACEBASE)
                                        unknownStackStorage = true;
                                }
                                else {
                                    StackNode nextNode = new StackNode(outVn, curNode.offset,
                                        curNode.traversals | StackNode.IndexType.nonconstant_index);
                                    if (nextNode.iter != nextNode.vn.endDescend()) {
                                        outVn.setMark();
                                        path.Add(nextNode);
                                        markedVn.Add(outVn);
                                    }
                                    else if (outVn.getSpace().getType() == spacetype.IPTR_SPACEBASE)
                                        unknownStackStorage = true;
                                }
                                break;
                            }
                        case OpCode.CPUI_SEGMENTOP:
                            {
                                if (op.getIn(2) != curNode.vn) break;  // Check that stackpointer comes in as inner pointer
                                                                        // Treat output as having the same offset, fallthru to COPY
                            }
                        case OpCode.CPUI_INDIRECT:
                        case OpCode.CPUI_COPY:
                            {
                                StackNode nextNode = new StackNode(outVn, curNode.offset, curNode.traversals);
                                if (nextNode.iter != nextNode.vn.endDescend()) {
                                    outVn.setMark();
                                    path.Add(nextNode);
                                    markedVn.Add(outVn);
                                }
                                else if (outVn.getSpace().getType() == spacetype.IPTR_SPACEBASE)
                                    unknownStackStorage = true;
                                break;
                            }
                        case OpCode.CPUI_MULTIEQUAL:
                            {
                                StackNode nextNode = new StackNode(outVn, curNode.offset,
                                    curNode.traversals | StackNode.IndexType.multiequal);
                                if (nextNode.iter != nextNode.vn.endDescend()) {
                                    outVn.setMark();
                                    path.Add(nextNode);
                                    markedVn.Add(outVn);
                                }
                                else if (outVn.getSpace().getType() == spacetype.IPTR_SPACEBASE)
                                    unknownStackStorage = true;
                                break;
                            }
                        case OpCode.CPUI_LOAD:
                            {
                                // Note that if ANY path has one of the traversals (non-constant ADD or MULTIEQUAL), then
                                // THIS path must have one of the traversals, because the only other acceptable path elements
                                // (INDIRECT/COPY/constant ADD) have only one path through.
                                if (curNode.traversals != 0) {
                                    generateLoadGuard(curNode, op, spc);
                                }
                                break;
                            }
                        case OpCode.CPUI_STORE:
                            {
                                if (op.getIn(1) == curNode.vn) {
                                    // Make sure the STORE pointer comes from our path
                                    if (curNode.traversals != 0) {
                                        generateStoreGuard(curNode, op, spc);
                                    }
                                    else {
                                        // If there were no traversals (of non-constant ADD or MULTIEQUAL) then the
                                        // pointer is equal to the stackpointer plus a constant (through an indirect is possible)
                                        // This will likely get resolved in the next heritage pass, but we leave the
                                        // spacebaseptr mark on, so that that the indirects don't get removed
                                        fd.opMarkSpacebasePtr(op);
                                    }
                                }
                                break;
                            }
                        default:
                            break;
                    }
                }
            }
            for (int i = 0; i < markedVn.size(); ++i)
                markedVn[i].clearMark();
            if (unknownStackStorage && checkFreeStores)
                return protectFreeStores(spc, freeStores);
            return false;
        }

        /// \brief Revisit STOREs with free pointers now that a heritage pass has completed
        ///
        /// We regenerate STORE LoadGuard records then cross-reference with STOREs that were
        /// originally free to see if they actually needed a LoadGaurd.  If not, the STORE
        /// is unmarked and INDIRECTs it has caused are removed.
        /// \param spc is the address space being guarded
        /// \param freeStores is the list of STOREs that were marked as free
        private void reprocessFreeStores(AddrSpace spc, List<PcodeOp> freeStores)
        {
            for (int i = 0; i < freeStores.size(); ++i)
                fd.opClearSpacebasePtr(freeStores[i]);
            discoverIndexedStackPointers(spc, freeStores, false);
            for (int i = 0; i < freeStores.size(); ++i) {
                PcodeOp op = freeStores[i];

                // If the STORE now is marked as using a spacebase ptr, then it was appropriately
                // marked to begin with, and we don't need to clean anything up
                if (op.usesSpacebasePtr()) continue;

                // If not the STORE may have triggered INDIRECTs that are unnecessary
                PcodeOp? indOp = op.previousOp();
                while (indOp != (PcodeOp)null) {
                    if (indOp.code() != OpCode.CPUI_INDIRECT) break;
                    Varnode iopVn = indOp.getIn(1);
                    if (iopVn.getSpace().getType() != spacetype.IPTR_IOP) break;
                    if (op != PcodeOp.getOpFromConst(iopVn.getAddr())) break;
                    PcodeOp nextOp = indOp.previousOp();
                    if (indOp.getOut().getSpace() == spc) {
                        fd.totalReplace(indOp.getOut(), indOp.getIn(0));
                        fd.opDestroy(indOp);       // Get rid of the INDIRECT
                    }
                    indOp = nextOp;
                }
            }
        }

        /// \brief Normalize p-code ops so that phi-node placement and renaming works
        ///
        /// The traditional phi-node placement and renaming algorithms don't expect
        /// variable pairs where there is partial overlap. For the given address range,
        /// we make all the free Varnode sizes look uniform by adding PIECE and SUBPIECE
        /// ops. We also add INDIRECT ops, so that we can ignore indirect effects
        /// of LOAD/STORE/CALL ops.
        /// \param addr is the starting address of the given range
        /// \param size is the number of bytes in the given range
        /// \param guardPerformed is true if a guard has been previously performed on the range
        /// \param read is the set of Varnode values reading from the range
        /// \param write is the set of written Varnodes in the range
        /// \param inputvars is the set of Varnodes in the range already marked as input
        private void guard(Address addr, int size, bool guardPerformed,
           List<Varnode> read, List<Varnode> write, List<Varnode> inputvars)
        {
            Varnode.varnode_flags fl;
            Varnode vn;
            IEnumerator<Varnode> iter;

            for (int index = 0; index < read.Count; index++) {
                vn = read[index];
                if (vn.getSize() < size)
                    read[index] = vn = normalizeReadSize(vn, addr, size);
                vn.setActiveHeritage();
            }

            for (int index = 0; index < write.Count; index++) {
                vn = write[index];
                if (vn.getSize() < size)
                    write[index] = vn = normalizeWriteSize(vn, addr, size);
                vn.setActiveHeritage();
            }

            // The full syntax tree may form over several stages, so we see a new
            // free for an address that has already been guarded before.
            // Because INDIRECTs for a single CALL or STORE really issue simultaneously, having multiple INDIRECT guards
            // for the same address confuses the renaming algorithm, so we don't add guards if we've added them before.
            if (!guardPerformed) {
                fl = 0;
                // Query for generic properties of address (use empty usepoint)
                fd.getScopeLocal().queryProperties(addr, size, new Address(), out fl);
                guardCalls(fl, addr, size, write);
                guardReturns(fl, addr, size, write);
                if (fd.getArch().highPtrPossible(addr, size)) {
                    guardStores(addr, size, write);
                    guardLoads(fl, addr, size, write);
                }
            }
        }

        /// \brief Make sure existing inputs for the given range fill it entirely
        ///
        /// The method is provided any Varnodes that overlap the range and are
        /// already marked as input.  If there are any holes in coverage, new
        /// input Varnodes are created to cover them. A final unified Varnode
        /// covering the whole range is built out of the pieces. In any event,
        /// things are set up so the renaming algorithm sees only a single Varnode.
        /// \param addr is the first address in the given range
        /// \param size is the number of bytes in the range
        /// \param input are the pre-existing inputs, given in address order
        private void guardInput(Address addr, int size, List<Varnode> input)
        {
            if (input.empty()) return;
            // If there is only one input and it fills everything
            // it will get linked in automatically
            if ((input.size() == 1) && (input[0].getSize() == size)) return;

            // Otherwise we need to make sure there are no holes
            int i = 0;
            ulong cur = addr.getOffset();   // Range that needs to be covered
            ulong end = cur + (uint)size;
            //  bool seenunspliced = false;
            Varnode vn;
            List<Varnode> newinput = new List<Varnode>();

            // Make sure the input range is filled
            while (cur < end) {
                if (i < input.size()) {
                    vn = input[i];
                    if (vn.getOffset() > cur) {
                        int sz = (int)(vn.getOffset() - cur);
                        vn = fd.newVarnode(sz, new Address(addr.getSpace(), cur));
                        vn = fd.setInputVarnode(vn);
                        //	seenunspliced = true;
                    }
                    else {
                        //	if (vn.hasNoDescend())
                        //	  seenunspliced = true;
                        i += 1;
                    }
                }
                else {
                    int sz = (int)(end - cur);
                    vn = fd.newVarnode(sz, new Address(addr.getSpace(), cur));
                    vn = fd.setInputVarnode(vn);
                    //      seenunspliced = true;
                }
                newinput.Add(vn);
                cur += (uint)vn.getSize();
            }

            // Now we need to make sure that all the inputs get linked
            // together into a single input
            if (newinput.size() == 1) return; // Will get linked in automatically
            for (int j = 0; j < newinput.size(); ++j)
                newinput[j].setWriteMask();
            //   if (!seenunspliced) {
            //     // Check to see if a concatenation of inputs already exists
            //     // If it existed already it would be defined at fd.getAddress()
            //     // and it would have full size
            //     IEnumerator<Varnode> iter,enditer;
            //     iter = fd.beginLoc(size,addr,fd.getAddress());
            //     enditer = fd.endLoc(size,addr,fd.getAddress());
            //     if (iter != enditer) return; // It already exists
            //   }
            Varnode newout = fd.newVarnode(size, addr);
            concatPieces(newinput, (PcodeOp)null, newout).setActiveHeritage();
        }

        /// \brief Guard an address range that is larger than any single parameter
        ///
        /// In this situation, an address range is being heritaged, but only a piece of
        /// it can be a parameter for a given call. We have to construct a SUBPIECE that
        /// pulls out the potential parameter.
        /// \param fc is the call site potentially taking a parameter
        /// \param addr is the starting address of the range
        /// \param transAddr is the start of the same range from the callee's stack perspective
        /// \param size is the size of the range in bytes
        private void guardCallOverlappingInput(FuncCallSpecs fc, Address addr, Address transAddr,
            int size)
        {
            VarnodeData vData = new VarnodeData();

            if (fc.getBiggestContainedInputParam(transAddr, size, vData)) {
                ParamActive active = fc.getActiveInput();
                Address truncAddr = new Address(vData.space, vData.offset);
                if (active.whichTrial(truncAddr, size) < 0) {
                    // If not already a trial
                    int truncateAmount = transAddr.justifiedContain(size, truncAddr, (int)vData.size, false);
                    int diff = (int)(truncAddr.getOffset() - transAddr.getOffset());
                    truncAddr = addr + diff;        // Convert truncated Address to caller's perspective
                    PcodeOp op = fc.getOp();
                    PcodeOp subpieceOp = fd.newOp(2, op.getAddr());
                    fd.opSetOpcode(subpieceOp, OpCode.CPUI_SUBPIECE);
                    Varnode wholeVn = fd.newVarnode(size, addr);
                    wholeVn.setActiveHeritage();
                    fd.opSetInput(subpieceOp, wholeVn, 0);
                    fd.opSetInput(subpieceOp, fd.newConstant(4, (ulong)truncateAmount), 1);
                    Varnode vn = fd.newVarnodeOut((int)vData.size, truncAddr, subpieceOp);
                    fd.opInsertBefore(subpieceOp, op);
                    active.registerTrial(truncAddr, (int)vData.size);
                    fd.opInsertInput(op, vn, op.numInput());
                }
            }
        }

        /// \brief Guard an address range that is larger than the possible output storage
        ///
        /// A potential return value should look like an \b indirect \b creation at this stage,
        /// but the range is even bigger.  We split it up into 2 or 3 Varnodes, and make each one via
        /// an INDIRECT.  The piece corresponding to the potential return value is registered, and all
        /// the pieces are concatenated to form a Varnode of the whole range.
        /// \param fc is the call site potentially returning a value
        /// \param addr is the starting address of the range
        /// \param size is the size of the range in bytes
        /// \param write is the set of new written Varnodes
        /// \return \b true if the INDIRECTs were created
        private bool guardCallOverlappingOutput(FuncCallSpecs fc, Address addr, int size,
            List<Varnode> write)
        {
            VarnodeData vData = new VarnodeData();

            if (!fc.getBiggestContainedOutput(addr, size, vData))
                return false;
            ParamActive active = fc.getActiveOutput();
            Address truncAddr = new Address(vData.space, vData.offset);
            if (active.whichTrial(truncAddr, size) >= 0)
                return false;       // Trial already exists
            int sizeFront = (int)(vData.offset - addr.getOffset());
            int sizeBack = size - vData.size - sizeFront;
            PcodeOp indOp = fd.newIndirectCreation(fc.getOp(), truncAddr, (int)vData.size, true);
            Varnode vnCollect = indOp.getOut();
            PcodeOp insertPoint = fc.getOp();
            if (sizeFront != 0) {
                PcodeOp indOpFront = fd.newIndirectCreation(indOp, addr, sizeFront, false);
                Varnode newFront = indOpFront.getOut();
                PcodeOp concatFront = fd.newOp(2, indOp.getAddr());
                int slotNew = vData.space.isBigEndian() ? 0 : 1;
                fd.opSetOpcode(concatFront, OpCode.CPUI_PIECE);
                fd.opSetInput(concatFront, newFront, slotNew);
                fd.opSetInput(concatFront, vnCollect, 1 - slotNew);
                vnCollect = fd.newVarnodeOut((int)(sizeFront + vData.size), addr, concatFront);
                fd.opInsertAfter(concatFront, insertPoint);
                insertPoint = concatFront;
            }
            if (sizeBack != 0) {
                Address addrBack = truncAddr + vData.size;
                PcodeOp indOpBack = fd.newIndirectCreation(fc.getOp(), addrBack, sizeBack, false);
                Varnode newBack = indOpBack.getOut();
                PcodeOp concatBack = fd.newOp(2, indOp.getAddr());
                int slotNew = vData.space.isBigEndian() ? 1 : 0;
                fd.opSetOpcode(concatBack, OpCode.CPUI_PIECE);
                fd.opSetInput(concatBack, newBack, slotNew);
                fd.opSetInput(concatBack, vnCollect, 1 - slotNew);
                vnCollect = fd.newVarnodeOut(size, addr, concatBack);
                fd.opInsertAfter(concatBack, insertPoint);
            }
            vnCollect.setActiveHeritage();
            write.Add(vnCollect);
            active.registerTrial(truncAddr, (int)vData.size);
            return true;
        }

        /// \brief Guard CALL/CALLIND ops in preparation for renaming algorithm
        ///
        /// For the given address range, we decide what the data-flow effect is
        /// across each call site in the function.  If an effect is unknown, an
        /// INDIRECT op is added, prepopulating data-flow through the call.
        /// Any new INDIRECT causes a new Varnode to be added to the \b write list.
        /// \param fl are any boolean properties associated with the address range
        /// \param addr is the first address of given range
        /// \param size is the number of bytes in the range
        /// \param write is the list of written Varnodes in the range (may be updated)
        private void guardCalls(Varnode.varnode_flags fl, Address addr, int size, List<Varnode> write)
        {
            FuncCallSpecs fc;
            PcodeOp indop;
            EffectRecord.EffectType effecttype;

            bool holdind = ((fl & Varnode.varnode_flags.addrtied) != 0);
            for (int i = 0; i < fd.numCalls(); ++i) {
                fc = fd.getCallSpecs(i);
                if (fc.getOp().isAssignment()) {
                    Varnode vn = fc.getOp().getOut();
                    if ((vn.getAddr() == addr) && (vn.getSize() == size)) continue;
                }
                effecttype = fc.hasEffectTranslate(addr, size);
                bool possibleoutput = false;
                if (fc.isOutputActive()) {
                    ParamActive active = fc.getActiveOutput();
                    ParamEntry.Containment outputCharacter = fc.characterizeAsOutput(addr, size);
                    if (outputCharacter != ParamEntry.Containment.no_containment) {
                        // A potential output is always killed by call
                        effecttype = EffectRecord.EffectType.killedbycall;
                        if (outputCharacter == ParamEntry.Containment.contained_by) {
                            if (guardCallOverlappingOutput(fc, addr, size, write))
                                // Range is handled, don't do additional guarding
                                effecttype = EffectRecord.EffectType.unaffected;
                        }
                        else {
                            if (active.whichTrial(addr, size) < 0) {
                                // If not already a trial
                                active.registerTrial(addr, size);
                                possibleoutput = true;
                            }
                        }
                    }
                }
                if (fc.isInputActive()) {
                    AddrSpace spc = addr.getSpace();
                    ulong off = addr.getOffset();
                    bool tryregister = true;
                    if (spc.getType() == spacetype.IPTR_SPACEBASE) {
                        if (fc.getSpacebaseOffset() != FuncCallSpecs.offset_unknown)
                            off = spc.wrapOffset(off - fc.getSpacebaseOffset());
                        else
                            // Do not attempt to register this stack loc as a trial
                            tryregister = false;
                    }
                    // Address relative to callee's stack
                    Address transAddr = new Address(spc, off);
                    if (tryregister) {
                        ParamEntry.Containment inputCharacter =
                            fc.characterizeAsInputParam(transAddr, size);
                        if (inputCharacter == ParamEntry.Containment.contains_justified) {
                            // Call could be using this range as an input parameter
                            ParamActive active = fc.getActiveInput();
                            if (active.whichTrial(transAddr, size) < 0) {
                                // If not already a trial
                                PcodeOp op = fc.getOp();
                                active.registerTrial(transAddr, size);
                                Varnode vn = fd.newVarnode(size, addr);
                                vn.setActiveHeritage();
                                fd.opInsertInput(op, vn, op.numInput());
                            }
                        }
                        else if (inputCharacter == ParamEntry.Containment.contained_by) {
                            // Call may be using part of this range as an input parameter
                            guardCallOverlappingInput(fc, addr, transAddr, size);
                        }
                    }
                }
                // We do not guard the call if the effect is "unaffected" or "reload"
                if (   (effecttype == EffectRecord.EffectType.unknown_effect)
                    || (effecttype == EffectRecord.EffectType.return_address))
                {
                    indop = fd.newIndirectOp(fc.getOp(), addr, size, 0);
                    indop.getIn(0).setActiveHeritage();
                    indop.getOut().setActiveHeritage();
                    write.Add(indop.getOut());
                    if (holdind) {
                        indop.getOut().setAddrForce();
                    }
                    if (effecttype == EffectRecord.EffectType.return_address) {
                        indop.getOut().setReturnAddress();
                    }
                }
                else if (effecttype == EffectRecord.EffectType.killedbycall) {
                    indop = fd.newIndirectCreation(fc.getOp(), addr, size, possibleoutput);
                    indop.getOut().setActiveHeritage();
                    write.Add(indop.getOut());
                }
            }
        }

        /// \brief Guard STORE ops in preparation for the renaming algorithm
        ///
        /// Depending on the pointer, a STORE operation may affect data-flow across the
        /// given address range. This method adds an INDIRECT op, prepopulating
        /// data-flow across the STORE.
        /// Any new INDIRECT causes a new Varnode to be added to the \b write list.
        /// \param addr is the first address of the given range
        /// \param size is the number of bytes in the given range
        /// \param write is the list of written Varnodes in the range (may be updated)
        private void guardStores(Address addr, int size, List<Varnode> write)
        {
            PcodeOp op, indop;
            AddrSpace spc = addr.getSpace();
            AddrSpace? container = spc.getContain();

            IEnumerator<PcodeOp> iter = fd.beginOp(OpCode.CPUI_STORE);
            while (iter.MoveNext()) {
                op = iter.Current;
                if (op.isDead()) continue;
                AddrSpace storeSpace = op.getIn(0).getSpaceFromConst();
                if ((container == storeSpace && op.usesSpacebasePtr()) || (spc == storeSpace)) {
                    indop = fd.newIndirectOp(op, addr, size, PcodeOp.Flags.indirect_store);
                    indop.getIn(0).setActiveHeritage();
                    indop.getOut().setActiveHeritage();
                    write.Add(indop.getOut());
                }
            }
        }

        /// \brief Guard LOAD ops in preparation for the renaming algorithm
        ///
        /// The op must be in the loadGuard list, which means it may pull values from an indexed
        /// range on the stack.  A COPY guard is placed for the given range on any LOAD op whose
        /// indexed range it intersects.
        /// \param fl is boolean properties associated with the address
        /// \param addr is the first address of the given range
        /// \param size is the number of bytes in the given range
        /// \param write is the list of written Varnodes in the range (may be updated)
        private void guardLoads(Varnode.varnode_flags fl, Address addr, int size, List<Varnode> write)
        {
            PcodeOp copyop;

            // If not address tied, don't consider for index alias
            if ((fl & Varnode.varnode_flags.addrtied) == 0) return;
            for(int index = 0; index < loadGuard.Count; index++) {
                LoadGuard guardRec = loadGuard[index];
                if (!guardRec.isValid(OpCode.CPUI_LOAD)) {
                    loadGuard.RemoveAt(index--);
                    continue;
                }
                if (guardRec.spc != addr.getSpace()) continue;
                if (addr.getOffset() < guardRec.minimumOffset) continue;
                if (addr.getOffset() > guardRec.maximumOffset) continue;
                copyop = fd.newOp(1, guardRec.op.getAddr());
                Varnode vn = fd.newVarnodeOut(size, addr, copyop);
                vn.setActiveHeritage();
                vn.setAddrForce();
                fd.opSetOpcode(copyop, OpCode.CPUI_COPY);
                Varnode invn = fd.newVarnode(size, addr);
                invn.setActiveHeritage();
                fd.opSetInput(copyop, invn, 0);
                fd.opInsertBefore(copyop, guardRec.op);
                loadCopyOps.Add(copyop);
            }
        }

        /// \brief Guard data-flow at RETURN ops, where range properly contains potention return storage
        ///
        /// The RETURN ops need to take a new input because of the potential of a return value,
        /// but the range is too big so it must be truncated to fit.
        /// \param addr is the starting address of the range
        /// \param size is the size of the range in bytes
        private void guardReturnsOverlapping(Address addr, int size)
        {
            VarnodeData vData = new VarnodeData();

            if (!fd.getFuncProto().getBiggestContainedOutput(addr, size, vData))
                return;
            Address truncAddr = new Address(vData.space, vData.offset);
            ParamActive active = fd.getActiveOutput();
            active.registerTrial(truncAddr, (int)vData.size);
            int offset = (int)(vData.offset - addr.getOffset());  // Number of least significant bytes to truncate
            if (vData.space.isBigEndian())
                offset = (int)((size - vData.size) - offset);
            IEnumerator<PcodeOp> iter = fd.beginOp(OpCode.CPUI_RETURN);
            while (iter.MoveNext()) {
                PcodeOp op = iter.Current;
                if (op.isDead()) continue;
                if (op.getHaltType() != 0) continue; // Special halt points cannot take return values
                Varnode invn = fd.newVarnode(size, addr);
                PcodeOp subOp = fd.newOp(2, op.getAddr());
                fd.opSetOpcode(subOp, OpCode.CPUI_SUBPIECE);
                fd.opSetInput(subOp, invn, 0);
                fd.opSetInput(subOp, fd.newConstant(4, (ulong)offset), 1);
                fd.opInsertBefore(subOp, op);
                Varnode retVal = fd.newVarnodeOut((int)vData.size, truncAddr, subOp);
                invn.setActiveHeritage();
                fd.opInsertInput(op, retVal, op.numInput());
            }
        }

        /// \brief Guard global data-flow at RETURN ops in preparation for renaming
        ///
        /// For the given global (persistent) address range, data-flow must persist up to
        /// (beyond) the end of the function. This method prepopulates data-flow for the
        /// range at all the RETURN ops, in order to enforce this.  Either a Varnode
        /// is added as input to the RETURN (for possible return values), or a COPY
        /// is inserted right before the RETURN with its output marked as
        /// \b address \b forced.
        /// \param fl are any boolean properties associated with the address range
        /// \param addr is the first address of the given range
        /// \param size is the number of bytes in the range
        /// \param write is the list of written Varnodes in the range (unused)
        private void guardReturns(Varnode.varnode_flags fl, Address addr, int size, List<Varnode> write)
        {
            PcodeOp op, copyop;

            ParamActive? active = fd.getActiveOutput();
            if (active != (ParamActive)null) {
                ParamEntry.Containment outputCharacter = fd.getFuncProto().characterizeAsOutput(addr, size);
                if (outputCharacter == ParamEntry.Containment.contained_by)
                    guardReturnsOverlapping(addr, size);
                else if (outputCharacter != ParamEntry.Containment.no_containment) {
                    active.registerTrial(addr, size);
                    IEnumerator<PcodeOp> iter = fd.beginOp(OpCode.CPUI_RETURN);
                    while (iter.MoveNext()) {
                        op = iter.Current;
                        if (op.isDead()) continue;
                        if (op.getHaltType() != 0) continue; // Special halt points cannot take return values
                        Varnode invn = fd.newVarnode(size, addr);
                        invn.setActiveHeritage();
                        fd.opInsertInput(op, invn, op.numInput());
                    }
                }
            }
            if ((fl & Varnode.varnode_flags.persist) == 0) return;
            IEnumerator<PcodeOp> iter = fd.beginOp(OpCode.CPUI_RETURN);
            while (iter.MoveNext()) {
                op = iter.Current;
                if (op.isDead()) continue;
                copyop = fd.newOp(1, op.getAddr());
                Varnode vn = fd.newVarnodeOut(size, addr, copyop);
                vn.setAddrForce();
                vn.setActiveHeritage();
                fd.opSetOpcode(copyop, OpCode.CPUI_COPY);
                copyop.setStopCopyPropagation();
                Varnode invn = fd.newVarnode(size, addr);
                invn.setActiveHeritage();
                fd.opSetInput(copyop, invn, 0);
                fd.opInsertBefore(copyop, op);
            }
        }

        /// \brief Build a refinement array given an address range and a list of Varnodes
        ///
        /// The array is a preallocated array of ints, one for each byte in the address
        /// range. Each Varnode in the given list has a 1 entered in the refinement
        /// array, at the position corresponding to the starting address of the Varnode
        /// and at the position corresponding to the address immediately following the
        /// Varnode.
        /// \param refine is the refinement array
        /// \param addr is the starting address of the given range
        /// \param size is the number of bytes in the range
        /// \param vnlist is the list of Varnodes to add to the array
        private static void buildRefinement(List<int> refine, Address addr, int size,
            List<Varnode> vnlist)
        {
            for (int i = 0; i < vnlist.size(); ++i) {
                Address curaddr = vnlist[i].getAddr();
                int sz = vnlist[i].getSize();
                int diff = (int)(curaddr.getOffset() - addr.getOffset());
                refine[diff] = 1;
                refine[diff + sz] = 1;
            }
        }

        /// \brief Split up a Varnode by the given \e refinement
        ///
        /// The \e refinement array is an array of integers, one for each byte in the
        /// given range. Any non-zero entry is the size of a particular element of the
        /// refinement starting at that corresponding byte in the range. I.e. the array
        /// [4,0,0,0,4,0,0,0] indicates the address range is 8-bytes long covered by
        /// two elements of length 4, starting at offsets 0 and 4 respectively.
        /// The given Varnode must be contained in the address range that the
        /// refinement array describes.
        ///
        /// A new set of Varnode pieces are returned in the \b split container, where
        /// the pieces form a disjoint cover of the original Varnode, and where the
        /// piece boundaries match the refinement.
        /// \param vn is the given Varnode to split
        /// \param addr is the starting address of the range described by the refinement
        /// \param refine is the refinement array
        /// \param split will hold the new Varnode pieces
        private void splitByRefinement(Varnode vn, Address addr, List<int> refine,
            List<Varnode> split)
        {
            Address curaddr = vn.getAddr();
            int sz = vn.getSize();
            AddrSpace spc = curaddr.getSpace();
            int diff = (int)spc.wrapOffset(curaddr.getOffset() - addr.getOffset());
            int cutsz = refine[diff];
            if (sz <= cutsz) return;    // Already refined
            while (sz > 0) {
                Varnode vn2 = fd.newVarnode(cutsz, curaddr);
                split.Add(vn2);
                curaddr = curaddr + cutsz;
                sz -= cutsz;
                diff = (int)spc.wrapOffset(curaddr.getOffset() - addr.getOffset());
                cutsz = refine[diff];
                if (cutsz > sz)
                    cutsz = sz;     // Final piece
            }
        }

        /// \brief Split up a \b free Varnode based on the given refinement
        ///
        /// The \e refinement array is an array of integers, one for each byte in the
        /// given range. Any non-zero entry is the size of a particular element of the
        /// refinement starting at that corresponding byte in the range. I.e. the array
        /// [4,0,0,0,4,0,0,0] indicates the address range is 8-bytes long covered by
        /// two elements of length 4, starting at offsets 0 and 4 respectively.
        ///
        /// If the Varnode overlaps the refinement, it is replaced with 2 or more
        /// covering Varnodes with boundaries that are on the refinement.  A concatenation
        /// expression is formed reconstructing the original value from the pieces. The
        /// original Varnode is replaced, in its p-code op, with a temporary Varnode that
        /// is the final output of the concatenation expression.
        /// \param vn is the given Varnode to split
        /// \param addr is the starting address of the address range being refined
        /// \param refine is the refinement array
        /// \param newvn is preallocated space for the holding the array of Varnode pieces
        private void refineRead(Varnode vn, Address addr, List<int> refine, List<Varnode> newvn)
        {
            newvn.Clear();
            splitByRefinement(vn, addr, refine, newvn);
            if (newvn.empty()) return;
            Varnode replacevn = fd.newUnique(vn.getSize());
            PcodeOp op = vn.loneDescend() ?? throw new BugException(); // Read is free so has 1 and only 1 descend
            int slot = op.getSlot(vn);
            concatPieces(newvn, op, replacevn);
            fd.opSetInput(op, replacevn, slot);
            if (vn.hasNoDescend())
                fd.deleteVarnode(vn);
            else
                throw new LowlevelError("Refining non-free varnode");
        }

        /// \brief Split up an output Varnode based on the given refinement
        ///
        /// The \e refinement array is an array of integers, one for each byte in the
        /// given range. Any non-zero entry is the size of a particular element of the
        /// refinement starting at that corresponding byte in the range. I.e. the array
        /// [4,0,0,0,4,0,0,0] indicates the address range is 8-bytes long covered by
        /// two elements of length 4, starting at offsets 0 and 4 respectively.
        ///
        /// If the Varnode overlaps the refinement, it is replaced with 2 or more
        /// covering Varnodes with boundaries that are on the refinement.  These pieces
        /// may be supplemented with additional pieces to obtain a disjoint cover of the
        /// entire address range.  A defining SUBPIECE op is generated for each piece.
        /// The original Varnode is replaced with a temporary Varnode.
        /// \param vn is the given Varnode to split
        /// \param addr is the starting address of the address range being refined
        /// \param refine is the refinement array
        /// \param newvn is preallocated space for the holding the array of Varnode pieces
        private void refineWrite(Varnode vn, Address addr, List<int> refine, List<Varnode> newvn)
        {
            newvn.Clear();
            splitByRefinement(vn, addr, refine, newvn);
            if (newvn.empty()) return;
            Varnode replacevn = fd.newUnique(vn.getSize());
            PcodeOp def = vn.getDef() ?? throw new BugException();
            fd.opSetOutput(def, replacevn);
            splitPieces(newvn, def, vn.getAddr(), vn.getSize(), replacevn);
            fd.totalReplace(vn, replacevn);
            fd.deleteVarnode(vn);
        }

        /// \brief Split up a known input Varnode based on the given refinement
        ///
        /// The \e refinement array is an array of integers, one for each byte in the
        /// given range. Any non-zero entry is the size of a particular element of the
        /// refinement starting at that corresponding byte in the range. I.e. the array
        /// [4,0,0,0,4,0,0,0] indicates the address range is 8-bytes long covered by
        /// two elements of length 4, starting at offsets 0 and 4 respectively.
        ///
        /// If the Varnode overlaps the refinement, it is replaced with 2 or more
        /// covering Varnodes with boundaries that are on the refinement.  These pieces
        /// may be supplemented with additional pieces to obtain a disjoint cover of the
        /// entire address range.  A defining SUBPIECE op is generated for each piece.
        /// \param vn is the given Varnode to split
        /// \param addr is the starting address of the address range being refined
        /// \param refine is the refinement array
        /// \param newvn is preallocated space for the holding the array of Varnode pieces
        private void refineInput(Varnode vn, Address addr, List<int> refine, List<Varnode> newvn)
        {
            newvn.Clear();
            splitByRefinement(vn, addr, refine, newvn);
            if (newvn.empty()) return;
            splitPieces(newvn, (PcodeOp)null, vn.getAddr(), vn.getSize(), vn);
            vn.setWriteMask();
        }

        /// \brief If we see 1-3 or 3-1 pieces in the partition, replace with a 4
        ///
        /// A refinement of a 4-byte range into a 1-byte and 3-byte cover is highly likely
        /// to be artificial, so we eliminate this configuration.
        ///
        /// The \e refinement array is an array of integers, one for each byte in the
        /// given range. Any non-zero entry is the size of a particular element of the
        /// refinement starting at that corresponding byte in the range. I.e. the array
        /// [4,0,0,0,4,0,0,0] indicates the address range is 8-bytes long covered by
        /// two elements of length 4, starting at offsets 0 and 4 respectively.
        /// \param refine is the refinement array
        private void remove13Refinement(List<int> refine)
        {
            if (refine.empty()) return;
            int pos = 0;
            int lastsize = refine[pos];
            int cursize;

            pos += lastsize;
            while (pos < refine.size()) {
                cursize = refine[pos];
                if (cursize == 0) break;
                if (((lastsize == 1) && (cursize == 3)) || ((lastsize == 3) && (cursize == 1))) {
                    refine[pos - lastsize] = 4;
                    lastsize = 4;
                    pos += cursize;
                }
                else {
                    lastsize = cursize;
                    pos += lastsize;
                }
            }

        }

        /// \brief Find the common refinement of all reads and writes in the address range
        ///
        /// Split the reads and writes so they match the refinement.
        /// \param addr is the first address in the range
        /// \param size is the number of bytes in the range
        /// \param readvars is all \e free Varnodes overlapping the address range
        /// \param writevars is all written Varnodes overlapping the address range
        /// \param inputvars is all known input Varnodes overlapping the address range
        /// \return \b true if there is a non-trivial refinement
        private bool refinement(Address addr, int size, List<Varnode> readvars,
            List<Varnode> writevars, List<Varnode> inputvars)
        {
            if (size > 1024) return false;
            List<int> refine = new List<int>(size+1);
            buildRefinement(refine, addr, size, readvars);
            buildRefinement(refine, addr, size, writevars);
            buildRefinement(refine, addr, size, inputvars);
            int lastpos = 0;
            for (int curpos = 1; curpos < size; ++curpos) {
                // Convert boundary points to partition sizes
                if (refine[curpos] != 0) {
                    refine[lastpos] = curpos - lastpos;
                    lastpos = curpos;
                }
            }
            if (lastpos == 0) return false; // No non-trivial refinements
            refine[lastpos] = size - lastpos;
            remove13Refinement(refine);
            List<Varnode> newvn = new List<Varnode>();
            for (int i = 0; i < readvars.size(); ++i)
                refineRead(readvars[i], addr, refine, newvn);
            for (int i = 0; i < writevars.size(); ++i)
                refineWrite(writevars[i], addr, refine, newvn);
            for (int i = 0; i < inputvars.size(); ++i)
                refineInput(inputvars[i], addr, refine, newvn);

            // Alter the disjoint cover (both locally and globally) to reflect our refinement
            LocationMap::iterator iter = disjoint.find(addr);
            int addrPass = (*iter).second.pass;
            disjoint.erase(iter.Key);
            iter = globaldisjoint.find(addr);
            globaldisjoint.erase(iter.Key);
            Address curaddr = addr;
            int cut = 0;
            int intersect;
            while (cut < size) {
                int sz = refine[cut];
                disjoint.add(curaddr, sz, addrPass, out intersect);
                globaldisjoint.add(curaddr, sz, addrPass, out intersect);
                cut += sz;
                curaddr = curaddr + sz;
            }
            return true;
        }

        /// \brief The heart of the phi-node placement algorithm
        ///
        /// Recursively walk the dominance tree starting from a given block.
        /// Calculate any children that are in the dominance frontier and add
        /// them to the \b merge array.
        /// \param qnode is the parent of the given block
        /// \param vnode is the given block
        private void visitIncr(FlowBlock qnode, FlowBlock vnode)
        {
            int k;
            FlowBlock v, child;

            int i = vnode.getIndex();
            int j = qnode.getIndex();
            IEnumerator<FlowBlock> iter = augment[i].GetEnumerator();
            while (iter.MoveNext()) {
                v = iter.Current;
                if (v.getImmedDom().getIndex() < j) {
                    // If idom(v) is strict ancestor of qnode
                    k = v.getIndex();
                    if ((flags[k] & heritage_flags.merged_node) == 0) {
                        merge.Add(v);
                        flags[k] |= heritage_flags.merged_node;
                    }
                    if ((flags[k] & heritage_flags.mark_node) == 0) {
                        // If v is not marked
                        flags[k] |= heritage_flags.mark_node;  // then mark it
                        pq.insert(v, depth[k]); // insert it into the queue
                    }
                }
                else
                    break;
            }
            if ((flags[i] & heritage_flags.boundary_node) == 0) {
                // If vnode is not a boundary node
                for (j = 0; j < domchild[i].size(); ++j) {
                    child = domchild[i][j];
                    if ((flags[child.getIndex()] & heritage_flags.mark_node) == 0)    // If the child is not marked
                        visitIncr(qnode, child);
                }
            }
        }

        /// \brief Calculate blocks that should contain MULTIEQUALs for one address range
        ///
        /// This is the main entry point for the phi-node placement algorithm. It is
        /// provided the normalized list of written Varnodes in this range.
        /// All refinement and guarding must already be performed for the Varnodes, and
        /// the dominance tree and its augmentation must already be computed.
        /// After this executes, the \b merge array holds blocks that should contain
        /// a MULTIEQUAL.
        /// \param write is the list of written Varnodes
        private void calcMultiequals(List<Varnode> write)
        {
            pq.reset(maxdepth);
            merge.Clear();

            int i, j;
            FlowBlock bl;
            // Place write blocks into the pq
            for (i = 0; i < write.size(); ++i) {
                bl = write[i].getDef().getParent(); // Get block where this write occurs
                j = bl.getIndex();
                if ((flags[j] & heritage_flags.mark_node) != 0) continue; // Already put in
                pq.insert(bl, depth[j]);    // Insert input node into priority queue
                flags[j] |= heritage_flags.mark_node;  // mark input node
            }
            if ((flags[0] & heritage_flags.mark_node) == 0)
            { // Make sure start node is in input
                pq.insert(fd.getBasicBlocks().getBlock(0), depth[0]);
                flags[0] |= heritage_flags.mark_node;
            }

            while (!pq.empty())
            {
                bl = pq.extract();      // Extract the next block
                visitIncr(bl, bl);
            }
            for (i = 0; i < flags.size(); ++i)
                flags[i] &= ~(heritage_flags.mark_node | heritage_flags.merged_node); // Clear marks from nodes
        }

        /// \brief The heart of the renaming algorithm.
        ///
        /// From the given block, recursively walk the dominance tree. At each
        /// block, visit the PcodeOps in execution order looking for Varnodes that
        /// need to be renamed.  As write Varnodes are encountered, a set of stack
        /// containers, differentiated by the Varnode's address, are updated so the
        /// so the current \e active Varnode is always ready for any \e free Varnode that
        /// is encountered. In this was all \e free Varnodes are replaced with the
        /// appropriate write Varnode or are promoted to a formal \e input Varnode.
        /// \param bl is the current basic block in the dominance tree walk
        /// \param varstack is the system of stacks, organized by address
        private void renameRecurse(BlockBasic bl, VariableStack varstack)
        {
            List<Varnode> writelist = new List<Varnode>(); // List varnodes that are written in this block
            BlockBasic subbl;
            IEnumerator<PcodeOp> suboiter;
            PcodeOp op;
            PcodeOp multiop;
            Varnode? vnout;
            Varnode vnin;
            Varnode vnnew;
            int i, slot;

            IEnumerator<PcodeOp> oiter = bl.beginOp();
            while (oiter.MoveNext()) {
                op = oiter.Current;
                if (op.code() != OpCode.CPUI_MULTIEQUAL) {
                    // First replace reads with top of stack
                    for (slot = 0; slot < op.numInput(); ++slot) {
                        vnin = op.getIn(slot);
                        if (vnin.isHeritageKnown()) continue; // not free
                        if (!vnin.isActiveHeritage()) continue; // Not being heritaged this round
                        vnin.clearActiveHeritage();
                        List<Varnode> stack = varstack[vnin.getAddr()];
                        if (stack.empty()) {
                            vnnew = fd.newVarnode(vnin.getSize(), vnin.getAddr());
                            vnnew = fd.setInputVarnode(vnnew);
                            stack.Add(vnnew);
                        }
                        else
                            vnnew = stack.GetLastItem();
                        // INDIRECTs and their op really happen AT SAME TIME
                        if (vnnew.isWritten() && (vnnew.getDef().code() == OpCode.CPUI_INDIRECT)) {
                            if (PcodeOp.getOpFromConst(vnnew.getDef().getIn(1).getAddr()) == op) {
                                if (stack.size() == 1) {
                                    vnnew = fd.newVarnode(vnin.getSize(), vnin.getAddr());
                                    vnnew = fd.setInputVarnode(vnnew);
                                    stack.Insert(0, vnnew);
                                }
                                else
                                    vnnew = stack[stack.size() - 2];
                            }
                        }
                        fd.opSetInput(op, vnnew, slot);
                        if (vnin.hasNoDescend())
                            fd.deleteVarnode(vnin);
                    }
                }
                // Then push writes onto stack
                vnout = op.getOut();
                if (vnout == (Varnode)null) continue;
                if (!vnout.isActiveHeritage()) continue; // Not a normalized write
                vnout.clearActiveHeritage();
                varstack[vnout.getAddr()].Add(vnout); // Push write onto stack
                writelist.Add(vnout);
            }
            for (i = 0; i < bl.sizeOut(); ++i) {
                subbl = (BlockBasic)bl.getOut(i);
                slot = bl.getOutRevIndex(i);
                suboiter = subbl.beginOp();
                while (suboiter.MoveNext()) {
                    multiop = suboiter.Current;
                    if (multiop.code() != OpCode.CPUI_MULTIEQUAL) break; // For each MULTIEQUAL
                    vnin = multiop.getIn(slot);
                    if (!vnin.isHeritageKnown()) {
                        List<Varnode> stack = varstack[vnin.getAddr()];
                        if (stack.empty()) {
                            vnnew = fd.newVarnode(vnin.getSize(), vnin.getAddr());
                            vnnew = fd.setInputVarnode(vnnew);
                            stack.Add(vnnew);
                        }
                        else
                            vnnew = stack.GetLastItem();
                        fd.opSetInput(multiop, vnnew, slot);
                        if (vnin.hasNoDescend())
                            fd.deleteVarnode(vnin);
                    }
                }
            }
            // Now we recurse to subtrees
            i = bl.getIndex();
            for (slot = 0; slot < domchild[i].Count; ++slot)
                renameRecurse((BlockBasic)domchild[i][slot], varstack);
            // Now we pop this blocks writes of the stack
            for (i = 0; i < writelist.Count; ++i) {
                vnout = writelist[i];
                varstack[vnout.getAddr()].RemoveLastItem();
            }
        }

        /// \brief Increase the heritage delay for the given AddrSpace and request a restart
        ///
        /// If applicable, look up the heritage stats for the address space
        /// and increment the delay.  The address space must allow an additional
        /// delay and can only be incremented once.  If the increment succeeds, the
        /// function is marked as having a \e restart pending.
        /// \param spc is the given AddrSpace
        private void bumpDeadcodeDelay(AddrSpace spc)
        {
            if ((spc.getType() != spacetype.IPTR_PROCESSOR) && (spc.getType() != spacetype.IPTR_SPACEBASE))
                return;         // Not the right kind of space
            if (spc.getDelay() != spc.getDeadcodeDelay())
                return;         // there is already a global delay
            if (fd.getOverride().hasDeadcodeDelay(spc))
                return;         // A delay has already been installed
            fd.getOverride().insertDeadcodeDelay(spc, spc.getDeadcodeDelay() + 1);
            fd.setRestartPending(true);
        }

        /// \brief Perform phi-node placement for the current set of address ranges
        ///
        /// Main entry point for performing the phi-node placement algorithm.
        /// Assume \b disjoint is filled with all the free Varnodes to be heritaged
        private void placeMultiequals()
        {
            List<Varnode> readvars = new List<Varnode>();
            List<Varnode> writevars = new List<Varnode>();
            List<Varnode> inputvars = new List<Varnode>();
            List<Varnode> removevars = new List<Varnode>();

            Dictionary<Address, SizePass>.Enumerator iter = disjoint.begin();
            while (iter.MoveNext()) {
                Address addr = iter.Current.Key;
                int size = iter.Current.Value.size;
                bool guardPerformed = iter.Current.Value.pass < pass;
                readvars.Clear();
                writevars.Clear();
                inputvars.Clear();
                removevars.Clear();
                // Collect reads/writes
                int max = collect(addr, size, readvars, writevars, inputvars, removevars);
                if ((size > 4) && (max < size)) {
                    if (refinement(addr, size, readvars, writevars, inputvars)) {
                        iter = disjoint.find(addr);
                        size = (*iter).second.size;
                        readvars.C();
                        writevars.Clear();
                        inputvars.Clear();
                        removevars.Clear();
                        collect(addr, size, readvars, writevars, inputvars, removevars);
                    }
                }
                if (readvars.empty()) {
                    if (writevars.empty() && inputvars.empty()) {
                        continue;
                    }
                    if (addr.getSpace().getType() == spacetype.IPTR_INTERNAL || guardPerformed) {
                        continue;
                    }
                }
                if (!removevars.empty()) {
                    removeRevisitedMarkers(removevars, addr, size);
                }
                guardInput(addr, size, inputvars);
                guard(addr, size, guardPerformed, readvars, writevars, inputvars);
                calcMultiequals(writevars); // Calculate where MULTIEQUALs go
                for (int i = 0; i < merge.size(); ++i) {
                    BlockBasic bl = (BlockBasic)merge[i];
                    PcodeOp multiop = fd.newOp(bl.sizeIn(), bl.getStart());
                    Varnode vnout = fd.newVarnodeOut(size, addr, multiop);
                    vnout.setActiveHeritage();
                    fd.opSetOpcode(multiop, OpCode.CPUI_MULTIEQUAL); // Create each MULTIEQUAL
                    for (int j = 0; j < bl.sizeIn(); ++j) {
                        Varnode vnin = fd.newVarnode(size, addr);
                        fd.opSetInput(multiop, vnin, j);
                    }
                    fd.opInsertBegin(multiop, bl); // Insert at beginning of block
                }
            }
            merge.Clear();
        }

        /// \brief Perform the renaming algorithm for the current set of address ranges
        ///
        /// Phi-node placement must already have happened.
        private void rename()
        {
            VariableStack varstack = new VariableStack();
            renameRecurse((BlockBasic)fd.getBasicBlocks().getBlock(0), varstack);
            disjoint.clear();
        }

        /// Instantiate the heritage manager for a particular function.
        /// \param data is the function
        public Heritage(Funcdata data)
        {
            fd = data;
            pass = 0;
            maxdepth = -1;
        }

        /// Get overall count of heritage passes
        public int getPass() => pass;

        /// \brief Get the pass number when the given address was heritaged
        /// \param addr is the given address
        /// \return the pass number or -1 if the address has not been heritaged
        public int heritagePass(Address addr) => globaldisjoint.findPass(addr);

        /// \brief Get the number times heritage was performed for the given address space
        ///
        /// A negative number indicates the number of passes to wait before the first
        /// heritage will occur.
        /// \param spc is the given address space
        /// \return the number of heritage passes performed
        public int numHeritagePasses(AddrSpace spc)
        {
            HeritageInfo info = getInfo(spc);
            if (!info.isHeritaged())
                throw new LowlevelError("Trying to calculate passes for non-heritaged space");
            return (pass - info.delay);
        }

        /// Inform system of dead code removal in given space 
        /// Record that Varnodes have been removed from the given space so that we can
        /// tell if there is any new heritage \e after the dead code removal.
        /// \param spc is the given address space
        public void seenDeadCode(AddrSpace spc)
        {
            getInfo(spc).deadremoved = 1;
        }

        /// Get pass delay for heritaging the given space
        /// Linking in Varnodes can be delayed for specific address spaces (to make sure all
        /// Varnodes for the space have been generated. Return the number of \e passes to
        /// delay for the given space.  0 means no delay.
        /// \param spc is the given address space
        /// \return the number of passes heritage is delayed
        public int getDeadCodeDelay(AddrSpace spc)
        {
            HeritageInfo info = getInfo(spc);
            return info.deadcodedelay;
        }

        /// Set delay for a specific space
        /// Set the number of heritage passes that are skipped before allowing dead code
        /// removal for Varnodes in the given address space (to make sure all Varnodes have
        /// been linked in before deciding what is dead).
        /// \param spc is the given address space
        /// \param delay is the number of passes to delay
        public void setDeadCodeDelay(AddrSpace spc, int delay)
        {
            HeritageInfo info = getInfo(spc);
            if (delay < info.delay)
                throw new LowlevelError("Illegal deadcode delay setting");
            info.deadcodedelay = delay;
        }

        /// Return \b true if it is \e safe to remove dead code
        /// Check if the required number of passes have transpired to allow removal of dead
        /// Varnodes in the given address space. If allowed, presumably no new Varnodes will
        /// be generated for the space.
        /// \param spc is the given address space
        /// \return \b true if dead code removal is allowed
        public bool deadRemovalAllowed(AddrSpace spc)
        {
            HeritageInfo info = getInfo(spc);
            return (pass > info.deadcodedelay);
        }

        /// \brief Check if dead code removal is safe and mark that removal has happened
        ///
        /// A convenience function combining deadRemovalAllowed() and seenDeadCode().
        /// Return \b true if it is \e safe to remove dead code, and, if so, also inform
        /// the system that dead code has happened for the given space.
        /// \param spc is the given address space
        /// \return \b true if dead code removal is allowed
        public bool deadRemovalAllowedSeen(AddrSpace spc)
        {
            HeritageInfo info = getInfo(spc);
            bool res = (pass > info.deadcodedelay);
            if (res)
                info.deadremoved = 1;
            return res;
        }

        /// Initialize information for each space
        /// This is called once to initialize \b this class in preparation for doing the
        /// heritage passes.  An information structure is allocated and mapped to each
        /// address space.
        public void buildInfoList()
        {
            if (!infolist.empty()) return;
            AddrSpaceManager manage = fd.getArch();
            infolist.reserve(manage.numSpaces());
            for (int i = 0; i < manage.numSpaces(); ++i)
                infolist.Add(new HeritageInfo(manage.getSpace(i)));
        }

        /// Force regeneration of basic block structures
        public void forceRestructure()
        {
            maxdepth = -1;
        }

        /// Reset all analysis of heritage
        /// Reset all analysis as if no heritage passes have yet taken place for the function.
        /// This does not directly affect Varnodes and PcodeOps in the underlying Funcdata.
        public void clear()
        {
            disjoint.clear();
            globaldisjoint.clear();
            domchild.Clear();
            augment.Clear();
            flags.Clear();
            depth.Clear();
            merge.Clear();
            clearInfoList();
            loadGuard.Clear();
            storeGuard.Clear();
            maxdepth = -1;
            pass = 0;
        }

        /// Perform one pass of heritage
        /// From any address space that is active for this pass, free Varnodes are collected
        /// and then fully integrated into SSA form.  Reads are connected to writes, inputs
        /// are identified, and phi-nodes are placed.
        public void heritage()
        {
            IEnumerator<Varnode> iter, enditer;
            HeritageInfo info;
            Varnode vn;
            bool needwarning;
            Varnode warnvn = (Varnode)null;
            int reprocessStackCount = 0;
            AddrSpace stackSpace = (AddrSpace)null;
            List<PcodeOp> freeStores = new List<PcodeOp>();
            PreferSplitManager splitmanage = new PreferSplitManager();

            if (maxdepth == -1)     // Has a restructure been forced
                buildADT();

            processJoins();
            if (pass == 0) {
                splitmanage.init(fd, fd.getArch().splitrecords);
                splitmanage.split();
            }
            for (int i = 0; i < infolist.size(); ++i) {
                info = infolist[i];
                if (!info.isHeritaged()) continue;
                if (pass < info.delay) continue; // It is too soon to heritage this space
                if (info.hasCallPlaceholders)
                    clearStackPlaceholders(info);

                if (!info.loadGuardSearch) {
                    info.loadGuardSearch = true;
                    if (discoverIndexedStackPointers(info.space, freeStores, true)) {
                        reprocessStackCount += 1;
                        stackSpace = info.space;
                    }
                }
                needwarning = false;
                iter = fd.beginLoc(info.space);
                enditer = fd.endLoc(info.space);

                while (iter != enditer) {
                    vn = *iter++;
                    if ((!vn.isWritten()) && vn.hasNoDescend() && (!vn.isUnaffected()) && (!vn.isInput()))
                        continue;
                    if (vn.isWriteMask()) continue;
                    int prev = 0;
                    LocationMap::iterator liter = globaldisjoint.add(vn.getAddr(), vn.getSize(), pass, prev);
                    if (prev == 0)      // All new location being heritaged, or intersecting with something new
                        disjoint.add((*liter).first, (*liter).second.size, pass, prev);
                    else if (prev == 2) {
                        // If completely contained in range from previous pass
                        if (vn.isHeritageKnown()) continue; // Don't heritage if we don't have to 
                        if (vn.hasNoDescend()) continue;
                        if ((!needwarning) && (info.deadremoved > 0) && !fd.isJumptableRecoveryOn()) {
                            needwarning = true;
                            bumpDeadcodeDelay(vn.getSpace());
                            warnvn = vn;
                        }
                        disjoint.add((*liter).first, (*liter).second.size, (*liter).second.pass, prev);
                    }
                    else {
                        // Partially contained in old range, but may contain new stuff
                        disjoint.add((*liter).first, (*liter).second.size, (*liter).second.pass, prev);
                        if ((!needwarning) && (info.deadremoved > 0) && !fd.isJumptableRecoveryOn()) {
                            // TODO: We should check if this varnode is tiled by previously heritaged ranges
                            if (vn.isHeritageKnown()) continue;        // Assume that it is tiled and produced by merging
                                                                        // In most cases, a truly new overlapping read will hit the bumpDeadcodeDelay either here or in prev==2
                            needwarning = true;
                            bumpDeadcodeDelay(vn.getSpace());
                            warnvn = vn;
                        }
                    }
                }

                if (needwarning) {
                    if (!info.warningissued) {
                        info.warningissued = true;
                        StringWriter errmsg = new StringWriter();
                        errmsg.Write("Heritage AFTER dead removal. Example location: ");
                        warnvn.printRawNoMarkup(errmsg);
                        if (!warnvn.hasNoDescend()) {
                            PcodeOp warnop = warnvn.beginDescend();
                            errmsg.Write(" : ");
                            warnop.getAddr().printRaw(errmsg);
                        }
                        fd.warningHeader(errmsg.ToString());
                    }
                }
            }
            placeMultiequals();
            rename();
            if (reprocessStackCount > 0)
                reprocessFreeStores(stackSpace, freeStores);
            analyzeNewLoadGuards();
            handleNewLoadCopies();
            if (pass == 0)
                splitmanage.splitAdditional();
            pass += 1;
        }

        /// Get list of LOAD ops that are guarded
        public List<LoadGuard> getLoadGuards() => loadGuard;

        /// Get list of STORE ops that are guarded
        public List<LoadGuard> getStoreGuards() => storeGuard;

        /// Get LoadGuard record associated with given PcodeOp
        /// \param op is the given PcodeOp
        /// \return the associated LoadGuard or NULL
        public LoadGuard? getStoreGuard(PcodeOp op)
        {
            foreach (LoadGuard guard in storeGuard) {
                if (guard.op == op)
                    return guard;
            }
            return (LoadGuard)null;
        }
    }
}
