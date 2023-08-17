using Sla.CORE;
using Sla.DECCORE;
using Sla.EXTRA;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Reflection.PortableExecutable;
using System.Runtime.Intrinsics;
using System.Text;
using System.Threading.Tasks;
using static System.Formats.Asn1.AsnWriter;

using ScopeMap = System.Collections.Generic.Dictionary<ulong, Sla.DECCORE.Scope>;

namespace Sla.DECCORE
{
    /// \brief Container for data structures associated with a single function
    ///
    /// This class holds the primary data structures for decompiling a function. In particular it holds
    /// control-flow, data-flow, and prototype information, plus class instances to help with constructing
    /// SSA form, structure control-flow, recover jump-tables, recover parameters, and merge Varnodes. In
    /// most cases it acts as the main API for querying and accessing these structures.
    ///
    /// Some important groups of public methods include:
    ///    - PcodeOp manipulation (mostly starting with 'op')
    ///    - PcodeOp search and traversal ('beginOp*' and 'endOp*')
    ///    - Varnode creation ('new*' methods)
    ///    - Varnode search and traversal ('beginLoc' 'endLoc' 'beginDef' and 'endDef')
    ///    - Basic block access and block structuring
    ///    - Access to subfunction prototypes
    ///    - Access to jump-tables (within the body of the function)
    internal class Funcdata
    {
        [Flags()]
        internal enum Flags
        {
            /// Set if Varnodes have HighVariables assigned
            highlevel_on = 1,
            /// Set if Basic blocks have been generated
            blocks_generated = 2,
            /// Set if at least one basic block is currently unreachable
            blocks_unreachable = 4,
            /// Set if processing has started
            processing_started = 8,
            /// Set if processing completed
            processing_complete = 0x10,
            /// Set if data-type analysis will be performed
            typerecovery_on = 0x20,
            /// Set if data-type recovery is started
            typerecovery_start = 0x40,
            /// Set if there is no code available for this function
            no_code = 0x80,
            /// Set if \b this Funcdata object is dedicated to jump-table recovery
            jumptablerecovery_on = 0x100,
            /// Don't try to recover jump-tables, always truncate
            jumptablerecovery_dont = 0x200,
            /// Analysis must be restarted (because of new override info)
            restart_pending = 0x400,
            /// Set if function contains unimplemented instructions
            unimplemented_present = 0x800,
            /// Set if function flowed into bad data
            baddata_present = 0x1000,
            /// Set if we are performing double precision recovery
            double_precis_on = 0x2000
        }

        /// Boolean properties associated with \b this function
        private Flags flags;
        /// Creation index of first Varnode created after start of cleanup
        private uint clean_up_index;
        /// Creation index of first Varnode created after HighVariables are created
        private uint high_level_index;
        /// Creation index of first Varnode created after ActionSetCasts
        private uint cast_phase_index;
        /// Minimum Varnode size to check as LanedRegister
        private uint minLanedSize;
        /// Number of bytes of binary data in function body
        private int size;
        /// Global configuration data
        private Architecture glb;
        /// The symbol representing \b this function
        private FunctionSymbol functionSymbol;
        /// Name of function
        private string name;
        /// Name to display in output
        private string displayName;
        /// Starting code address of binary data
        private Address baseaddr;
        /// Prototype of this function
        private FuncProto funcp;
        /// Local variables (symbols in the function scope)
        private ScopeLocal localmap;

        /// List of calls this function makes
        private List<FuncCallSpecs> qlst;
        /// List of jump-tables for this function
        private List<JumpTable> jumpvec;

        /// Container of Varnode objects for \b this function
        private VarnodeBank vbank;
        /// Container of PcodeOp objects for \b this function
        private PcodeOpBank obank;
        /// Unstructured basic blocks
        private BlockGraph bblocks;
        /// Structured block hierarchy (on top of basic blocks)
        private BlockGraph sblocks;
        /// Manager for maintaining SSA form
        private Heritage heritage;
        /// Variable range intersection algorithms
        private Merge covermerge;
        /// Data for assessing which parameters are passed to \b this function
        private ParamActive? activeoutput;
        /// Overrides of data-flow, prototypes, etc. that are local to \b this function
        private Override localoverride;
        /// Current storage locations which may be laned registers
        private Dictionary<VarnodeData, LanedRegister> lanedMap;
        /// A map from data-flow edges to the resolved field of TypeUnion being accessed
        private Dictionary<ResolveEdge, ResolvedUnion> unionMap;

        // Low level Varnode functions
        /// Look-up boolean properties and data-type information
        /// Properties of a given storage location are gathered from symbol information and
        /// applied to the given Varnode.
        /// \param vn is the given Varnode
        private void setVarnodeProperties(Varnode vn)
        {
            if (!vn.isMapped()) {
                // One more chance to find entry, now that we know usepoint
                Varnode.varnode_flags vflags;
                SymbolEntry? entry = localmap.queryProperties(vn.getAddr(), vn.getSize(), vn.getUsePoint(this),
                    out vflags);
                if (entry != (SymbolEntry)null) // Let entry try to force type
                    vn.setSymbolProperties(entry);
                else
                    vn.setFlags(vflags & ~Varnode.varnode_flags.typelock); // typelock set by updateType
            }

            if (vn.cover == (Cover)null) {
                if (isHighOn())
                    vn.calcCover();
            }
        }

        /// Assign a new HighVariable to a Varnode
        /// If HighVariables are enabled, make sure the given Varnode has one assigned. Allocate
        /// a dedicated HighVariable, that contains only the one Varnode if necessary.
        /// \param vn is the given Varnode
        /// \return the assigned HighVariable or NULL if one is not assigned
        private HighVariable? assignHigh(Varnode vn)
        {
            if ((flags & Flags.highlevel_on) != 0) {
                if (vn.hasCover())
                    vn.calcCover();
                if (!vn.isAnnotation()) {
                    return new HighVariable(vn);
                }
            }
            return (HighVariable)null;
        }

        /// Handle two variables with matching storage
        /// A Varnode overlaps the given SymbolEntry.  Make sure the Varnode is part of the variable
        /// underlying the Symbol.  If not, remap things so that the Varnode maps to a distinct Symbol.
        /// In either case, attach the appropriate Symbol to the Varnode
        /// \param entry is the given SymbolEntry
        /// \param vn is the overlapping Varnode
        /// \return the Symbol attached to the Varnode
        private Symbol handleSymbolConflict(SymbolEntry entry, Varnode vn)
        {
            if (vn.isInput() || vn.isAddrTied() ||
                vn.isPersist() || vn.isConstant() || entry.isDynamic())
            {
                vn.setSymbolEntry(entry);
                return entry.getSymbol();
            }
            HighVariable high = vn.getHigh();
            Varnode otherVn;
            HighVariable? otherHigh = (HighVariable)null;
            // Look for a conflicting HighVariable
            VarnodeLocSet::const_iterator iter = beginLoc(entry.getSize(), entry.getAddr());
            while (iter != endLoc()) {
                otherVn = *iter;
                if (otherVn.getSize() != entry.getSize()) break;
                if (otherVn.getAddr() != entry.getAddr()) break;
                HighVariable tmpHigh = otherVn.getHigh();
                if (tmpHigh != high) {
                    otherHigh = tmpHigh;
                    break;
                }
                ++iter;
            }
            if (otherHigh == (HighVariable)null) {
                vn.setSymbolEntry(entry);
                return entry.getSymbol();
            }

            // If we reach here, we have a conflicting variable
            buildDynamicSymbol(vn);
            return vn.getSymbolEntry().getSymbol();
        }

        /// \brief Update Varnode properties based on (new) Symbol information
        ///
        /// Boolean properties \b addrtied, \b addrforce, and \b nolocalalias
        /// for Varnodes are updated based on new Symbol information they map to.
        /// The caller can elect to update data-type information as well, where Varnodes
        /// and their associated HighVariables have their data-type finalized based symbols.
        /// \param lm is the Symbol scope within which to search for mapped Varnodes
        /// \param updateDatatypes is \b true if the caller wants to update data-types
        /// \param unmappedAliasCheck is \b true if an alias check should be performed on unmapped Varnodes
        /// \return \b true if any Varnode was updated
        private bool syncVarnodesWithSymbol(ScopeLocal lm, bool updateDatatypes, bool unmappedAliasCheck)
        {
            bool updateoccurred = false;
            VarnodeLocSet::const_iterator iter, enditer;
            Datatype? ct;
            SymbolEntry? entry;
            Varnode.varnode_flags fl;

            iter = vbank.beginLoc(lm.getSpaceId());
            enditer = vbank.endLoc(lm.getSpaceId());
            while (iter != enditer) {
                Varnode vnexemplar = *iter;
                entry = lm.findOverlap(vnexemplar.getAddr(), vnexemplar.getSize());
                ct = (Datatype)null;
                if (entry != (SymbolEntry)null) {
                    fl = entry.getAllFlags();
                    if (entry.getSize() >= vnexemplar.getSize()) {
                        if (updateDatatypes) {
                            ct = entry.getSizedType(vnexemplar.getAddr(), vnexemplar.getSize());
                            if (ct != (Datatype)null && ct.getMetatype() == type_metatype.TYPE_UNKNOWN)
                                ct = (Datatype)null;
                        }
                    }
                    else {
                        // Overlapping but not containing
                        // This is usual indicative of a small locked symbol
                        // getting put in a bigger register
                        // Don't try to figure out type
                        // Don't keep typelock and namelock
                        fl &= ~(Varnode.varnode_flags.typelock | Varnode.varnode_flags.namelock);
                        // we do particularly want to keep the nolocalalias
                    }
                }
                else {
                    // Could not find any symbol
                    if (lm.inScope(vnexemplar.getAddr(), vnexemplar.getSize(), vnexemplar.getUsePoint(this))) {
                        // This is technically an error, there should be some
                        // kind of symbol, if we are in scope
                        fl = Varnode.varnode_flags.mapped | Varnode.varnode_flags.addrtied;
                    }
                    else if (unmappedAliasCheck) {
                        // If the varnode is not in scope, check if we should treat as unaliased
                        fl = lm.isUnmappedUnaliased(vnexemplar) ? Varnode.varnode_flags.nolocalalias : 0;
                    }
                    else
                        fl = 0;
                }
                if (syncVarnodesWithSymbol(iter, fl, ct))
                    updateoccurred = true;
            }
            return updateoccurred;
        }

        /// Transform all reads of the given Varnode to a special \b undefined constant
        /// All p-code ops that read the Varnode are transformed so that they read
        /// a special constant instead (associate with unreachable block removal).
        /// \param vn is the given Varnode
        /// \return \b true if a PcodeOp is modified
        private bool descend2Undef(Varnode vn)
        {
            PcodeOp op, copyop;
            BlockBasic inbl;
            Varnode badconst;
            int i, sz;
            bool res;

            res = false;
            sz = vn.getSize();
            IEnumerator<PcodeOp> iter = vn.beginDescend();
            while (iter.MoveNext()) {
                op = iter.Current;       // Move to next in list before deletion
                if (op.getParent().isDead()) continue;
                if (op.getParent().sizeIn() != 0) res = true;
                i = op.getSlot(vn);
                badconst = newConstant(sz, 0xBADDEF);
                if (op.code() == OpCode.CPUI_MULTIEQUAL) {
                    // Cannot put constant directly into MULTIEQUAL
                    inbl = (BlockBasic)op.getParent().getIn(i);
                    copyop = newOp(1, inbl.getStart());
                    Varnode inputvn = newUniqueOut(sz, copyop);
                    opSetOpcode(copyop, OpCode.CPUI_COPY);
                    opSetInput(copyop, badconst, 0);
                    opInsertEnd(copyop, inbl);
                    opSetInput(op, inputvn, i);
                }
                else if (op.code() == OpCode.CPUI_INDIRECT) {
                    // Cannot put constant directly into INDIRECT
                    copyop = newOp(1, op.getAddr());
                    Varnode inputvn = newUniqueOut(sz, copyop);
                    opSetOpcode(copyop, OpCode.CPUI_COPY);
                    opSetInput(copyop, badconst, 0);
                    opInsertBefore(copyop, op);
                    opSetInput(op, inputvn, i);
                }
                else
                    opSetInput(op, badconst, i);
            }
            return res;
        }

        /// Make all reads of the given Varnode unique
        /// For the given Varnode, duplicate its defining PcodeOp at each read of the Varnode
        /// so that the read becomes a new unique Varnode. This operation should not be performed on any
        /// PcodeOp with side effects like OpCode.CPUI_CALL.
        /// \param vn is the given Varnode
        private void splitUses(Varnode vn)
        {
            PcodeOp op = vn.getDef() ?? throw new BugException();
            Varnode newvn;
            PcodeOp newop, useop;
            int slot;

            IEnumerator<PcodeOp> iter = vn.descend.GetEnumerator();
            if (!iter.MoveNext()) return; // No descendants at all
            useop = iter.Current;
            if (!iter.MoveNext()) return; // Only one descendant
            while (true) {
                slot = useop.getSlot(vn);      // Get first descendant
                newop = newOp(op.numInput(), op.getAddr());
                newvn = newVarnode(vn.getSize(), vn.getAddr(), vn.getType());
                opSetOutput(newop, newvn);
                opSetOpcode(newop, op.code());
                for (int i = 0; i < op.numInput(); ++i)
                    opSetInput(newop, op.getIn(i), i);
                opSetInput(useop, newvn, slot);
                opInsertBefore(newop, op);
                if (!iter.MoveNext()) break;
                useop = iter.Current;
            }
            // Dead-code actions should remove original op
        }

        /// Clone a Varnode (between copies of the function)
        /// Internal factory for copying Varnodes from another Funcdata object into \b this.
        /// \param vn is the Varnode to clone
        /// \return the cloned Varnode (contained by \b this)
        private Varnode cloneVarnode(Varnode vn)
        {
            Varnode newvn = vbank.create(vn.getSize(), vn.getAddr(), vn.getType());
            Varnode.varnode_flags vflags = vn.getFlags();
            // These are the flags we allow to be cloned
            vflags &= (Varnode.varnode_flags.annotation | Varnode.varnode_flags.externref |
                   Varnode.varnode_flags.@readonly | Varnode.varnode_flags.persist |
                   Varnode.varnode_flags.addrtied | Varnode.varnode_flags.addrforce |
                   Varnode.varnode_flags.indirect_creation | Varnode.varnode_flags.incidental_copy |
                   Varnode.varnode_flags.volatil | Varnode.varnode_flags.mapped);
            newvn.setFlags(vflags);
            return newvn;
        }

        /// Delete the given Varnode from \b this function
        /// References to the Varnode are replaced with NULL pointers and the object is freed,
        /// with no possibility of resuse.
        /// \param vn is the Varnode to delete
        private void destroyVarnode(Varnode vn)
        {
            IEnumerator<PcodeOp> iter = vn.beginDescend();

            while (iter.MoveNext()) {
                PcodeOp op = iter.Current;
#if OPACTION_DEBUG
                if (opactdbg_active)
                    debugModCheck(op);
#endif
                op.clearInput(op.getSlot(vn));
            }
            if (vn.def != (PcodeOp)null) {
                vn.def.setOutput((Varnode)null);
                vn.def = (PcodeOp)null;
            }
            vn.destroyDescend();
            vbank.destroy(vn);
        }

        /// \brief Make sure every Varnode in the given list has a Symbol it will link to
        ///
        /// This is used when Varnodes overlap a locked Symbol but extend beyond it.
        /// An existing Symbol is passed in with a list of possibly overextending Varnodes.
        /// The list is in Address order.  We check that each Varnode has a Symbol that
        /// overlaps its first byte (to guarantee a link). If one doesn't exist it is created.
        /// \param entry is the existing Symbol entry
        /// \param list is the list of Varnodes
        private void coverVarnodes(SymbolEntry entry, List<Varnode> list)
        {
            Scope scope = entry.getSymbol().getScope();
            for (int i = 0; i < list.size(); ++i) {
                Varnode vn = list[i];
                // We only need to check once for all Varnodes at the same Address
                // Of these, pick the biggest Varnode
                if (i + 1 < list.size() && list[i + 1].getAddr() == vn.getAddr())
                    continue;
                Address usepoint = vn.getUsePoint(this);
                SymbolEntry overlapEntry = scope.findContainer(vn.getAddr(), vn.getSize(), usepoint);
                if (overlapEntry == (SymbolEntry)null) {
                    int diff = (int)(vn.getOffset() - entry.getAddr().getOffset());
                    StringWriter s = new StringWriter();
                    s.Write($"{entry.getSymbol().getName()}_{diff}");
                    if (vn.isAddrTied())
                        usepoint = new Address();
                    scope.addSymbol(s.ToString(), vn.getHigh().getType(), vn.getAddr(), usepoint);
                }
            }
        }

        /// \brief Cache information from a UnionFacetSymbol
        ///
        /// The symbol forces a particular union field resolution for the associated PcodeOp and slot,
        /// which are extracted from the given \e dynamic SymbolEntry.  The resolution is cached
        /// in the \b unionMap so that it will get picked up by resolveInFlow() methods etc.
        /// \param entry is the given SymbolEntry
        /// \param dhash is preallocated storage for calculating the dynamic hash
        /// \return \b true if the UnionFacetSymbol is successfully cached
        private bool applyUnionFacet(SymbolEntry entry, DynamicHash dhash)
        {
            Symbol sym = entry.getSymbol();
            PcodeOp op = dhash.findOp(this, entry.getFirstUseAddress(), entry.getHash());
            if (op == (PcodeOp)null)
                return false;
            int slot = DynamicHash.getSlotFromHash(entry.getHash());
            int fldNum = ((UnionFacetSymbol)sym).getFieldNumber();
            ResolvedUnion resolve = new ResolvedUnion(sym.getType(), fldNum, glb.types);
            resolve.setLock(true);
            return setUnionField(sym.getType(), op, slot, resolve);
        }

        // Low level op functions
        /// Transform trivial OpCode.CPUI_MULTIEQUAL to OpCode.CPUI_COPY
        /// If the MULTIEQUAL has no inputs, presumably the basic block is unreachable, so we treat
        /// the p-code op as a COPY from a new input Varnode. If there is 1 input, the MULTIEQUAL
        /// is transformed directly into a COPY.
        /// \param op is the given MULTIEQUAL
        private void opZeroMulti(PcodeOp op)
        {
            if (op.numInput() == 0)
            {   // If no branches left
                opInsertInput(op, newVarnode(op.getOut().getSize(), op.getOut().getAddr()), 0);
                setInputVarnode(op.getIn(0));  // Then this is an input
                opSetOpcode(op, OpCode.CPUI_COPY);
            }
            else if (op.numInput() == 1)
                opSetOpcode(op, OpCode.CPUI_COPY);
        }

        // Low level block functions
        /// \brief Remove an active basic block from the function
        ///
        /// PcodeOps in the block are deleted.  Data-flow and control-flow are otherwise
        /// patched up. Most of the work is patching up MULTIEQUALs and other remaining
        /// references to Varnodes flowing through the block to be removed.
        ///
        /// If descendant Varnodes are stranded by removing the block, either an exception is
        /// thrown, or optionally, the descendant Varnodes can be replaced with constants and
        /// a warning is printed.
        /// \param bb is the given basic block
        /// \param unreachable is \b true if the caller wants a warning for stranded Varnodes
        private void blockRemoveInternal(BlockBasic bb, bool unreachable)
        {
            BlockBasic bbout;
            Varnode deadvn;
            PcodeOp deadop;
            int i, j, blocknum;
            bool desc_warning;

            PcodeOp? op = bb.lastOp();
            if ((op != (PcodeOp)null) && (op.code() == OpCode.CPUI_BRANCHIND)) {
                JumpTable? jt = findJumpTable(op);
                if (jt != (JumpTable)null)
                    removeJumpTable(jt);
            }
            IEnumerator<PcodeOp> iter;
            if (!unreachable) {
                // Make sure data flow is preserved
                pushMultiequals(bb);

                for (i = 0; i < bb.sizeOut(); ++i) {
                    bbout = (BlockBasic)bb.getOut(i);
                    if (bbout.isDead()) continue;
                    blocknum = bbout.getInIndex(bb); // Get index of bb into bbout
                    iter = bbout.beginOp();
                    while (iter.MoveNext()) {
                        op = iter.Current;
                        if (op.code() != OpCode.CPUI_MULTIEQUAL) continue;
                        deadvn = op.getIn(blocknum);
                        opRemoveInput(op, blocknum);    // Remove the deleted blocks branch
                        deadop = deadvn.getDef();
                        if (   deadvn.isWritten()
                            && (deadop.code() == OpCode.CPUI_MULTIEQUAL)
                            && (deadop.getParent() == bb))
                        {
                            // Append new branches
                            for (j = 0; j < bb.sizeIn(); ++j)
                                opInsertInput(op, deadop.getIn(j), op.numInput());
                        }
                        else {
                            for (j = 0; j < bb.sizeIn(); ++j)
                                opInsertInput(op, deadvn, op.numInput()); // Otherwise make copies
                        }
                        opZeroMulti(op);
                    }
                }
            }
            bblocks.removeFromFlow(bb);

            desc_warning = false;
            iter = bb.beginOp();
            while (iter.MoveNext()) {
                // Finally remove all the ops
                op = iter.Current;
                if (op.isAssignment()) {
                    // op still has some descendants
                    deadvn = op.getOut();
                    if (unreachable) {
                        bool undef = descend2Undef(deadvn);
                        if (undef && !desc_warning) {
                            // Mark descendants as undefined
                            warningHeader("Creating undefined varnodes in (possibly) reachable block");
                            desc_warning = true;    // Print the warning only once
                        }
                    }
                    if (descendantsOutside(deadvn)) // If any descendants outside of bb
                        throw new LowlevelError("Deleting op with descendants\n");
                }
                if (op.isCall())
                    deleteCallSpecs(op);
                // Increment iterator before unlinking
                opDestroy(op);      // No longer has descendants
            }
            bblocks.removeBlock(bb);    // Remove the block altogether
        }

        /// \brief Remove an outgoing branch of the given basic block
        ///
        /// MULTIEQUAL p-code ops (in other blocks) that take inputs from the outgoing branch
        /// are patched appropriately.
        /// \param bb is the given basic block
        /// \param num is the index of the outgoing edge to remove
        private void branchRemoveInternal(BlockBasic bb, int num)
        {
            BlockBasic bbout;
            PcodeOp op;
            int blocknum;

            if (bb.sizeOut() == 2) // If there is no decision left
                opDestroy(bb.lastOp());    // Remove the branch instruction

            bbout = (BlockBasic)bb.getOut(num);
            blocknum = bbout.getInIndex(bb);
            bblocks.removeEdge(bb, bbout); // Sever (one) connection between bb and bbout
            IEnumerator<PcodeOp> iter = bbout.beginOp();
            while (iter.MoveNext()) {
                op = iter.Current;
                if (op.code() != OpCode.CPUI_MULTIEQUAL) continue;
                opRemoveInput(op, blocknum);
                opZeroMulti(op);
            }
        }

        /// Push MULTIEQUAL Varnodes of the given block into the output block
        /// Assuming the given basic block is being removed, force any Varnode defined by
        /// a MULTIEQUAL in the block to be defined in the output block instead. This is used
        /// as part of the basic block removal process to patch up data-flow.
        /// \param bb is the given basic block
        private void pushMultiequals(BlockBasic bb)
        {
            PcodeOp origop, replaceop;
            Varnode origvn, replacevn;

            if (bb.sizeOut() == 0) return;
            if (bb.sizeOut() > 1)
                warningHeader("push_multiequal on block with multiple outputs");
            // Take first output block. If this is a donothing block, it is the only output block
            BlockBasic outblock = (BlockBasic)bb.getOut(0);
            int outblock_ind = bb.getOutRevIndex(0);
            IEnumerator<PcodeOp> iter = bb.beginOp();
            while (iter.MoveNext()) {
                origop = iter.Current;
                if (origop.code() != OpCode.CPUI_MULTIEQUAL) continue;
                origvn = origop.getOut();
                if (origvn.hasNoDescend()) continue;
                bool needreplace = false;
                bool neednewunique = false;
                IEnumerator<PcodeOp> citer = origvn.beginDescend();
                while (citer.MoveNext()) {
                    PcodeOp op = citer.Current;
                    if ((op.code() == OpCode.CPUI_MULTIEQUAL) && (op.getParent() == outblock)) {
                        bool deadEdge = true;   // Check for reference to origvn NOT thru the dead edge
                        for (int i = 0; i < op.numInput(); ++i) {
                            if (i == outblock_ind) continue;    // Not going thru dead edge
                            if (op.getIn(i) == origvn) {
                                // Reference to origvn
                                deadEdge = false;
                                break;
                            }
                        }
                        if (deadEdge) {
                            if ((origvn.getAddr() == op.getOut().getAddr()) && origvn.isAddrTied())
                                // If origvn is addrtied and feeds into a MULTIEQUAL at same address in outblock
                                // Then any use of origvn beyond outblock that did not go thru this MULTIEQUAL must have
                                // propagated through some other register.  So we make the new MULTIEQUAL write to a unique register
                                neednewunique = true;
                            continue;
                        }
                    }
                    needreplace = true;
                    break;
                }
                if (!needreplace) continue;
                // Construct artificial MULTIEQUAL
                List<Varnode> branches = new List<Varnode>();
                if (neednewunique)
                    replacevn = newUnique(origvn.getSize());
                else
                    replacevn = newVarnode(origvn.getSize(), origvn.getAddr());
                for (int i = 0; i < outblock.sizeIn(); ++i) {
                    if (outblock.getIn(i) == bb)
                        branches.Add(origvn);
                    else
                        branches.Add(replacevn);

                    // In this situation there are other blocks "beyond" outblock which read
                    // origvn defined in bb, but there are other blocks falling into outblock
                    // Assuming the only out of bb is outblock, all heritages of origvn must
                    // come through outblock.  Thus any alternate ins to outblock must be
                    // dominated by bb.  So the artificial MULTIEQUAL we construct must have
                    // all inputs be origvn
                }
                replaceop = newOp(branches.Count, outblock.getStart());
                opSetOpcode(replaceop, OpCode.CPUI_MULTIEQUAL);
                opSetOutput(replaceop, replacevn);
                opSetAllInput(replaceop, branches);
                opInsertBegin(replaceop, outblock);

                // Replace obsolete origvn with replacevn
                IEnumerator<PcodeOp> titer = origvn.descend.GetEnumerator();
                while (titer.MoveNext()) {
                    PcodeOp op = titer.Current;
                    int i = op.getSlot(origvn);
                    // Do not replace MULTIEQUAL references in the same block
                    // as replaceop.  These are patched by block_remove
                    if (   (op.code() == OpCode.CPUI_MULTIEQUAL)
                        && (op.getParent() == outblock)
                        && (i == outblock_ind))
                    {
                        continue;
                    }
                    opSetInput(op, replacevn, i);
                }
            }
        }

        /// Clear all basic blocks
        private void clearBlocks()
        {
            bblocks.clear();
            sblocks.clear();
        }

        /// Calculate initial basic block structures (after a control-flow change)
        /// For the current control-flow graph, (re)calculate the loop structure and dominance.
        /// This can be called multiple times as changes are made to control-flow.
        /// The structured hierarchy is also reset.
        private void structureReset()
        {
            List<FlowBlock> rootlist = new List<FlowBlock>();

            flags &= ~Flags.blocks_unreachable;   // Clear any old blocks flag
            bblocks.structureLoops(rootlist);
            bblocks.calcForwardDominator(rootlist);
            if (rootlist.Count > 1)
                flags |= Flags.blocks_unreachable;
            // Check for dead jumptables
            List<JumpTable> alivejumps = new List<JumpTable>();
            IEnumerator<JumpTable> iter = jumpvec.GetEnumerator();
            while (iter.MoveNext()) {
                JumpTable jt = iter.Current;
                PcodeOp indop = jt.getIndirectOp();
                if (indop.isDead()) {
                    warningHeader("Recovered jumptable eliminated as dead code");
                    delete jt;
                    continue;
                }
                alivejumps.Add(jt);
            }
            jumpvec = alivejumps;
            // Force structuring algorithm to start over
            sblocks.clear();
            // Make copy of the basic block control flow graph
            //  sblocks.build_copy(bblocks);
            heritage.forceRestructure();
        }

        /// \brief Recover a jump-table for a given BRANCHIND using existing flow information
        ///
        /// A partial function (copy) is built using the flow info. Simplification is performed on the
        /// partial function (using the "jumptable" strategy), then destination addresses of the
        /// branch are recovered by examining the simplified data-flow. The jump-table object
        /// is populated with the recovered addresses.  An integer value is returned:
        ///   - 0 = success
        ///   - 1 = normal could-not-recover failure
        ///   - 2 = \b likely \b thunk failure
        ///   - 3 = no legal flows to the BRANCHIND failure
        ///
        /// \param partial is a function object for caching analysis
        /// \param jt is the jump-table object to populate
        /// \param op is the BRANCHIND p-code op to analyze
        /// \param flow is the existing flow information
        /// \return the success/failure code
        private int stageJumpTable(Funcdata partial, JumpTable jt, PcodeOp op, FlowInfo flow)
        {
            if (!partial.isJumptableRecoveryOn())
            {
                // Do full analysis on the table if we haven't before
                partial.flags |= jumptablerecovery_on; // Mark that this Funcdata object is dedicated to jumptable recovery
                partial.truncatedFlow(this, flow);

                string oldactname = glb.allacts.getCurrentName(); // Save off old action
                try
                {
                    glb.allacts.setCurrent("jumptable");
#if OPACTION_DEBUG
                    if (jtcallback != (void(*)(Funcdata & orig, Funcdata & fd))0)
	(*jtcallback)(*this, partial);  // Alternative reset/perform
      else
                    {
#endif
                    glb.allacts.getCurrent().reset(partial);
                    glb.allacts.getCurrent().perform(partial); // Simplify the partial function
#if OPACTION_DEBUG
                    }
#endif
                    glb.allacts.setCurrent(oldactname); // Restore old action
                }
                catch (LowlevelError err) {
                    glb.allacts.setCurrent(oldactname);
                    warning(err.ToString(), op.getAddr());
                    return 1;
                }
            }
            PcodeOp? partop = partial.findOp(op.getSeqNum());

            if (   partop == (PcodeOp)null
                || partop.code() != OpCode.CPUI_BRANCHIND
                || partop.getAddr() != op.getAddr())
            {
                throw new LowlevelError("Error recovering jumptable: Bad partial clone");
            }
            if (partop.isDead())   // Indirectop we were trying to recover was eliminated as dead code (unreachable)
                return 0;           // Return jumptable as

            try {
                jt.setLoadCollect(flow.doesJumpRecord());
                jt.setIndirectOp(partop);
                if (jt.getStage() > 0)
                    jt.recoverMultistage(partial);
                else
                    jt.recoverAddresses(partial); // Analyze partial to recover jumptable addresses
            }
            catch (JumptableNotReachableError) {   // Thrown by recoverAddresses
                return 3;
            }
            catch (JumptableThunkError) {        // Thrown by recoverAddresses
                return 2;
            }
            catch (LowlevelError err) {
                warning(err.ToString(), op.getAddr());
                return 1;
            }
            return 0;
        }

        /// Convert jump-table addresses to basic block indices
        /// For each jump-table, for each address, the corresponding basic block index is computed.
        /// This also calculates the \e default branch for each jump-table.
        /// \param flow is the flow object (mapping addresses to p-code ops)
        private void switchOverJumpTables(FlowInfo flow)
        {
            IEnumerator<JumpTable> iter = jumpvec.GetEnumerator();

            while (iter.MoveNext())
                iter.Current.switchOver(flow);
        }

        /// Clear any jump-table information
        /// Any override information is preserved.
        private void clearJumpTables()
        {
            List<JumpTable> remain = new List<JumpTable>();
            IEnumerator<JumpTable> iter = jumpvec.GetEnumerator();

            while (iter.MoveNext()) {
                JumpTable jt = iter.Current;
                if (jt.isOverride()) {
                    jt.clear();        // Clear out any derived data
                    remain.Add(jt);   // Keep the override itself
                }
                else {
                    // delete jt;
                }
            }

            jumpvec = remain;
        }

        /// Sort calls using a dominance based order
        /// Calls are put in dominance order so that earlier calls get evaluated first.
        /// Order affects parameter analysis.
        private void sortCallSpecs()
        {
            qlst.Sort(compareCallspecs);
        }

        /// Remove the specification for a particular call
        /// This is used internally if a CALL is removed (because it is unreachable)
        /// \param op is the particular specification to remove
        private void deleteCallSpecs(PcodeOp op)
        {
            IEnumerator<FuncCallSpecs> iter = qlst.GetEnumerator();

            while (iter.MoveNext()) {
                FuncCallSpecs fc = iter.Current;
                if (fc.getOp() == op) {
                    qlst.Remove(fc);
                    // delete fc;
                    return;
                }
            }
        }

        /// Remove all call specifications
        private void clearCallSpecs()
        {
            // int i;

            //for (i = 0; i < qlst.size(); ++i)
            //    delete qlst[i];     // Delete each func_callspec

            qlst.Clear();           // Delete list of pointers
        }

        /// \brief Split given basic block b along an \e in edge
        ///
        /// A copy of the block is made, inheriting the same \e out edges but only the
        /// one indicated \e in edge, which is removed from the original block.
        /// Other data-flow is \b not affected.
        /// \param b is the given basic block
        /// \param inedge is the index of the indicated \e in edge
        private BlockBasic nodeSplitBlockEdge(BlockBasic b, int inedge)
        {
            FlowBlock a = b.getIn(inedge);
            BlockBasic bprime = bblocks.newBlockBasic(this);
            bprime.setFlag(FlowBlock.block_flags.f_duplicate_block);
            bprime.copyRange(b);
            bblocks.switchEdge(a, b, bprime);
            for (int i = 0; i < b.sizeOut(); ++i)
                bblocks.addEdge(bprime, b.getOut(i));
            return bprime;
        }

        /// \brief Duplicate the given PcodeOp as part of splitting a block
        ///
        /// Make a basic clone of the p-code op copying its basic control-flow properties
        /// \param op is the given PcodeOp
        /// \return the cloned op
        private PcodeOp nodeSplitCloneOp(PcodeOp op)
        {
            if (op.isBranch()) {
                if (op.code() != OpCode.CPUI_BRANCH)
                    throw new LowlevelError("Cannot duplicate 2-way or n-way branch in nodeplit");
                return (PcodeOp)null;
            }
            PcodeOp dup = newOp(op.numInput(), op.getAddr());
            opSetOpcode(dup, op.code());
            PcodeOp.Flags fl = op.flags & (PcodeOp.Flags.startbasic | PcodeOp.Flags.nocollapse |
                PcodeOp.Flags.startmark);
            dup.setFlag(fl);
            return dup;
        }

        /// \brief Duplicate output Varnode of the given p-code op, as part of splitting a block
        ///
        /// Make a basic clone of the Varnode and its basic flags. The clone is created
        /// as an output of a previously cloned PcodeOp.
        /// \param op is the given op whose output should be cloned
        /// \param newop is the cloned version
        private void nodeSplitCloneVarnode(PcodeOp op, PcodeOp newop)
        {
            Varnode? opvn = op.getOut();
            Varnode newvn;

            if (opvn == (Varnode)null) return;
            newvn = newVarnodeOut(opvn.getSize(), opvn.getAddr(), newop);
            Varnode.varnode_flags vflags = opvn.getFlags();
            vflags &= (Varnode.varnode_flags.externref | Varnode.varnode_flags.volatil |
                Varnode.varnode_flags.incidental_copy | Varnode.varnode_flags.@readonly |
                Varnode.varnode_flags.persist | Varnode.varnode_flags.addrtied |
                Varnode.varnode_flags.addrforce);
            newvn.setFlags(vflags);
        }

        /// \brief Clone all p-code ops from a block into its copy
        ///
        /// P-code in a basic block is cloned into the split version of the block.
        /// Only the output Varnodes are cloned, not the inputs.
        /// \param b is the original basic block
        /// \param bprime is the cloned block
        private void nodeSplitRawDuplicate(BlockBasic b, BlockBasic bprime)
        {
            PcodeOp b_op, prime_op;
            IEnumerator<PcodeOp> iter = b.beginOp();

            while (iter.MoveNext()) {
                b_op = iter.Current;
                prime_op = nodeSplitCloneOp(b_op);
                if (prime_op == (PcodeOp)null) continue;
                nodeSplitCloneVarnode(b_op, prime_op);
                opInsertEnd(prime_op, bprime);
            }
        }

        /// \brief Patch Varnode inputs to p-code ops in split basic block
        ///
        /// Map Varnodes that are inputs for PcodeOps in the original basic block to the
        /// input slots of the cloned ops in the split block. Constants and code ref Varnodes
        /// need to be duplicated, other Varnodes are shared between the ops. This routine
        /// also pulls an input Varnode out of riginal MULTIEQUAL ops and adds it back
        /// to the cloned MULTIEQUAL ops.
        /// \param b is the original basic block
        /// \param bprime is the split clone of the block
        /// \param inedge is the incoming edge index that was split on
        private void nodeSplitInputPatch(BlockBasic b, BlockBasic bprime, int inedge)
        {
            PcodeOp bop, pop;
            Varnode bvn;
            Varnode pvn;
            Dictionary<PcodeOp, PcodeOp> btop = new Dictionary<PcodeOp, PcodeOp>(); // Map from b to bprime
            List<PcodeOp> pind = new List<PcodeOp>();  // pop needing b input
            List<PcodeOp> bind = new List<PcodeOp>();  // bop giving input
            List<int> pslot = new List<int>();     // slot within pop needing b input

            IEnumerator<PcodeOp> biter = b.beginOp();
            IEnumerator<PcodeOp> piter = bprime.beginOp();

            while (piter != bprime.endOp()) {
                bop = biter.Current;
                pop = piter.Current;
                btop[bop] = pop;        // Establish mapping
                if (bop.code() == OpCode.CPUI_MULTIEQUAL) {
                    pop.setNumInputs(1);   // One edge now goes into bprime
                    opSetOpcode(pop, OpCode.CPUI_COPY);
                    opSetInput(pop, bop.getIn(inedge), 0);
                    opRemoveInput(bop, inedge); // One edge is removed from b
                    if (bop.numInput() == 1)
                        opSetOpcode(bop, OpCode.CPUI_COPY);
                }
                else if (bop.code() == OpCode.CPUI_INDIRECT) {
                    throw new LowlevelError("Can't handle INDIRECTs in nodesplit");
                }
                else if (bop.isCall()) {
                    throw new LowlevelError("Can't handle CALLs in nodesplit");
                }
                else {
                    for (int i = 0; i < pop.numInput(); ++i) {
                        bvn = bop.getIn(i);
                        if (bvn.isConstant())
                            pvn = newConstant(bvn.getSize(), bvn.getOffset());
                        else if (bvn.isAnnotation())
                            pvn = newCodeRef(bvn.getAddr());
                        else if (bvn.isFree())
                            throw new LowlevelError("Can't handle free varnode in nodesplit");
                        else {
                            if (bvn.isWritten()) {
                                if (bvn.getDef().getParent() == b) {
                                    pind.Add(pop); // Need a cross reference
                                    bind.Add(bvn.getDef());
                                    pslot.Add(i);
                                    continue;
                                }
                                else
                                    pvn = bvn;
                            }
                            else
                                pvn = bvn;
                        }
                        opSetInput(pop, pvn, i);
                    }
                }
                ++piter;
                ++biter;
            }

            for (int i = 0; i < pind.size(); ++i) {
                pop = pind[i];
                PcodeOp cross = btop[bind[i]];
                opSetInput(pop, cross.getOut(), pslot[i]);
            }
        }

        /// \brief Check if given Varnode has any descendants in a dead block
        ///
        /// Assuming a basic block is marked \e dead, return \b true if any PcodeOp reading
        /// the Varnode is in the dead block.
        /// \param vn is the given Varnode
        /// \return \b true if the Varnode is read in the dead block
        private static bool descendantsOutside(Varnode vn)
        {
            IEnumerator<PcodeOp> iter = vn.beginDescend();

            while (iter.MoveNext())
                if (!iter.Current.getParent().isDead()) return true;
            return false;
        }

        /// \brief Encode descriptions for a set of Varnodes to a stream
        ///
        /// This is an internal function for the function's marshaling system.
        /// Individual elements are written in sequence for Varnodes in a given set.
        /// The set is bounded by iterators using the 'loc' ordering.
        /// \param encoder is the stream encoder
        /// \param iter is the beginning of the set
        /// \param enditer is the end of the set
        private static void encodeVarnode(Sla.CORE.Encoder encoder, VarnodeLocSet::const_iterator iter,
            VarnodeLocSet::const_iterator enditer)
        {
            Varnode vn;
            while (iter != enditer) {
                vn = *iter++;
                vn.encode(encoder);
            }
        }

        /// \brief Check if the given Varnode only flows into call-based INDIRECT ops
        ///
        /// Flow is only followed through MULTIEQUAL ops.
        /// \param vn is the given Varnode
        /// \return \b true if all flows hit an INDIRECT op
        private static bool checkIndirectUse(Varnode vn)
        {
            List<Varnode> vlist = new List<Varnode>();
            int i = 0;
            vlist.Add(vn);
            vn.setMark();
            bool result = true;
            while ((i < vlist.Count) && result) {
                vn = vlist[i++];
                IEnumerator<PcodeOp> iter = vn.beginDescend();
                while (iter.MoveNext()) {
                    PcodeOp op = iter.Current;
                    OpCode opc = op.code();
                    if (opc == OpCode.CPUI_INDIRECT) {
                        if (op.isIndirectStore()) {
                            // INDIRECT from a STORE is not a negative result but continue to follow data-flow
                            Varnode outvn = op.getOut();
                            if (!outvn.isMark()) {
                                vlist.Add(outvn);
                                outvn.setMark();
                            }
                        }
                    }
                    else if (opc == OpCode.CPUI_MULTIEQUAL) {
                        Varnode outvn = op.getOut();
                        if (!outvn.isMark()) {
                            vlist.Add(outvn);
                            outvn.setMark();
                        }
                    }
                    else {
                        result = false;
                        break;
                    }
                }
            }
            for (i = 0; i < vlist.size(); ++i)
                vlist[i].clearMark();
            return result;
        }

        /// \brief Find the primary branch operation for an instruction
        ///
        /// For machine instructions that branch, this finds the \e primary PcodeOp that performs
        /// the branch.  The instruction is provided as a list of p-code ops, and the caller can
        /// specify whether they expect to see a \e branch, \e call, or \e return operation.
        /// \param iter is the start of the operations for the instruction
        /// \param enditer is the end of the operations for the instruction
        /// \param findbranch is \b true if the caller expects to see a BRANCH, CBRANCH, or BRANCHIND
        /// \param findcall is \b true if the caller expects to see CALL or CALLIND
        /// \param findreturn is \b true if the caller expects to see RETURN
        /// \return the first branching PcodeOp that matches the criteria or NULL
        private static PcodeOp? findPrimaryBranch(PcodeOpTree::const_iterator iter, PcodeOpTree::const_iterator enditer,
            bool findbranch, bool findcall, bool findreturn)
        {
            while (iter != enditer) {
                PcodeOp op = (*iter).second;
                switch (op.code()) {
                    case OpCode.CPUI_BRANCH:
                    case OpCode.CPUI_CBRANCH:
                        if (findbranch) {
                            if (!op.getIn(0).isConstant()) // Make sure this is not an internal branch
                                return op;
                        }
                        break;
                    case OpCode.CPUI_BRANCHIND:
                        if (findbranch)
                            return op;
                        break;
                    case OpCode.CPUI_CALL:
                    case OpCode.CPUI_CALLIND:
                        if (findcall)
                            return op;
                        break;
                    case OpCode.CPUI_RETURN:
                        if (findreturn)
                            return op;
                        break;
                    default:
                        break;
                }
                ++iter;
            }
            return (PcodeOp)null;
        }

        /// \param nm is the (base) name of the function
        /// \param scope is Symbol scope associated with the function
        /// \param addr is the entry address for the function
        /// \param sym is the symbol representing the function
        /// \param sz is the number of bytes (of code) in the function body
        public Funcdata(string nm, string disp, Scope conf, Address addr, FunctionSymbol sym, int sz = 0)
        {
            baseaddr = addr;
            funcp = new FuncProto();
            vbank = new VarnodeBank(conf.getArch());
            heritage = new Heritage(this);
            covermerge = new Merge(this);
            // Initialize high-level properties of
            // function by giving address and size
            functionSymbol = sym;
            flags = 0;
            clean_up_index = 0;
            high_level_index = 0;
            cast_phase_index = 0;
            glb = scope.getArch();
            minLanedSize = (uint)glb.getMinimumLanedRegisterSize();
            name = nm;
            displayName = disp;

            size = sz;
            AddrSpace stackid = glb.getStackSpace();
            if (nm.Length == 0)
                localmap = (ScopeLocal)null; // Filled in by decode
            else {
                ulong id;
                if (sym != (FunctionSymbol)null)
                    id = sym.getId();
                else {
                    // Missing a symbol, build unique id based on address
                    id = 0x57AB12CD;
                    id = (id << 32) | (addr.getOffset() & 0xffffffff);
                }
                ScopeLocal newMap = new ScopeLocal(id, stackid, this, glb);
                glb.symboltab.attachScope(newMap, scope);     // This may throw and delete newMap
                localmap = newMap;
                funcp.setScope(localmap, baseaddr + -1);
                localmap.resetLocalWindow();
            }
            activeoutput = (ParamActive)null;

#if OPACTION_DEBUG
            jtcallback = (void(*)(Funcdata & orig, Funcdata & fd))0;
            opactdbg_count = 0;
            opactdbg_breakcount = -1;
            opactdbg_on = false;
            opactdbg_breakon = false;
            opactdbg_active = false;
#endif
        }

        ~Funcdata()
        {
            //  clear();
            if (localmap != (ScopeLocal)null)
                glb.symboltab.deleteScope(localmap);

            clearCallSpecs();
            //for (int i = 0; i < jumpvec.size(); ++i) // Delete jumptables
            //    delete jumpvec[i];
            glb = (Architecture)null;
        }

        /// Get the function's local symbol name
        public string getName() => name;

        /// Get the name to display in output
        public string getDisplayName() => displayName;

        /// Get the entry point address
        public Address getAddress() => baseaddr;

        /// Get the function body size in bytes
        public int getSize() => size;

        /// Get the program/architecture owning \b this function
        public Architecture getArch() => glb;

        /// Return the symbol associated with \b this function
        public FunctionSymbol getSymbol() => functionSymbol;

        /// Are high-level variables assigned to Varnodes
        public bool isHighOn() => ((flags & Flags.highlevel_on) != 0);

        /// Has processing of the function started
        public bool isProcStarted() => ((flags & Flags.processing_started) != 0);

        /// Is processing of the function complete
        public bool isProcComplete() => ((flags & Flags.processing_complete) != 0);

        /// Did this function exhibit unreachable code
        public bool hasUnreachableBlocks() => ((flags & Flags.blocks_unreachable) != 0);

        ///< Will data-type analysis be performed
        public bool isTypeRecoveryOn() => ((flags & Flags.typerecovery_on) != 0);

        ///< Has data-type recovery processes started
        public bool hasTypeRecoveryStarted() => ((flags & Flags.typerecovery_start) != 0);

        ///< Return \b true if \b this function has no code body
        public bool hasNoCode() => ((flags & Flags.no_code) != 0);

        public void setNoCode(bool val)
        {
            if (val) flags |= Flags.no_code;
            else flags &= ~Flags.no_code;
        }   ///< Toggle whether \b this has a body

        ///< Mark that laned registers have been collected
        public void setLanedRegGenerated()
        {
            minLanedSize = 1000000;
        }

        /// \brief Toggle whether \b this is being used for jump-table recovery
        ///
        /// \param val is \b true to indicate a jump-table is being recovered
        public void setJumptableRecovery(bool val)
        {
            if (val) flags &= ~Flags.jumptablerecovery_dont;
            else flags |= Flags.jumptablerecovery_dont;
        }

        ///< Is \b this used for jump-table recovery
        public bool isJumptableRecoveryOn() => ((flags & Flags.jumptablerecovery_on) != 0);

        /// \brief Toggle whether double precision analysis is used
        ///
        /// \param val is \b true if double precision analysis is enabled
        public void setDoublePrecisRecovery(bool val)
        {
            if (val) flags |= Flags.double_precis_on;
            else flags &= ~Flags.double_precis_on;
        }

        ///< Is double precision analysis enabled
        public bool isDoublePrecisOn() => ((flags & Flags.double_precis_on) != 0);

        ///< Return \b true if no block structuring was performed
        public bool hasNoStructBlocks() => (sblocks.getSize() == 0);

        ///< Clear out old disassembly
        public void clear()
        {
            // Clear everything associated with decompilation (analysis)
            flags &= ~(Flags.highlevel_on | Flags.blocks_generated | Flags.processing_started | Flags.typerecovery_start
                | Flags.typerecovery_on | Flags.double_precis_on | Flags.restart_pending);
            clean_up_index = 0;
            high_level_index = 0;
            cast_phase_index = 0;
            minLanedSize = (uint)glb.getMinimumLanedRegisterSize();

            localmap.clearUnlocked();  // Clear non-permanent stuff
            localmap.resetLocalWindow();

            clearActiveOutput();
            funcp.clearUnlockedOutput();    // Inputs are cleared by localmap
            unionMap.Clear();
            clearBlocks();
            obank.clear();
            vbank.clear();
            clearCallSpecs();
            clearJumpTables();
            // Do not clear overrides
            heritage.clear();
            covermerge.clear();
#if OPACTION_DEBUG
            opactdbg_count = 0;
#endif
        }

        ///< Add a warning comment in the function body
        /// The comment is added to the global database, indexed via its placement address and
        /// the entry address of the function. The emitter will attempt to place the comment
        /// before the source expression that maps most closely to the address.
        /// \param txt is the string body of the comment
        /// \param ad is the placement address
        public void warning(string txt, Address ad)
        {
            string msg;
            if ((flags & Flags.jumptablerecovery_on) != 0)
                msg = "WARNING (jumptable): ";
            else
                msg = "WARNING: ";
            msg += txt;
            glb.commentdb.addCommentNoDuplicate(Comment.comment_type.warning, baseaddr, ad, msg);
        }

        /// Add a warning comment as part of the function header
        /// The warning will be emitted as part of the block comment printed right before the
        /// prototype. The comment is stored in the global comment database, indexed via the function's
        /// entry address.
        /// \param txt is the string body of the comment
        public void warningHeader(string txt)
        {
            string msg;
            if ((flags & Flags.jumptablerecovery_on) != 0)
                msg = "WARNING (jumptable): ";
            else
                msg = "WARNING: ";
            msg += txt;
            glb.commentdb.addCommentNoDuplicate(Comment.comment_type.warningheader, baseaddr, baseaddr, msg);
        }

        /// Start processing for this function
        /// This routine does basic set-up for analyzing the function. In particular, it
        /// generates the raw p-code, builds basic blocks, and generates the call specification
        /// objects.
        public void startProcessing()
        {
            if ((flags & Flags.processing_started) != 0)
                throw new LowlevelError("Function processing already started");
            flags |= Flags.processing_started;

            if (funcp.isInline())
                warningHeader("This is an inlined function");
            localmap.clearUnlocked();
            funcp.clearUnlockedOutput();
            Address baddr = new Address(baseaddr.getSpace(),0);
            Address eaddr = new Address(baseaddr.getSpace(),ulong.MaxValue);
            followFlow(baddr, eaddr);
            structureReset();
            sortCallSpecs();        // Must come after structure reset
            heritage.buildInfoList();
            localoverride.applyDeadCodeDelay(this);
        }

        /// Mark that processing has completed for this function
        public void stopProcessing()
        {
            flags |= Flags.processing_complete;
            obank.destroyDead();        // Free up anything in the dead list
#if CPUI_STATISTICS
            glb.stats.process(*this);
#endif
        }

        ///< Mark that data-type analysis has started
        public bool startTypeRecovery()
        {
            if ((flags & Flags.typerecovery_start) != 0) return false; // Already started
            flags |= Flags.typerecovery_start;
            return true;
        }

        /// \brief Toggle whether data-type recovery will be performed on \b this function
        /// \param val is \b true if data-type analysis is enabled
        public void setTypeRecovery(bool val)
        {
            flags = val ? (flags | Flags.typerecovery_on) : (flags & ~Flags.typerecovery_on);
        }

        public void startCastPhase()
        {
            cast_phase_index = vbank.getCreateIndex();
        }    ///< Start the \b cast insertion phase

        ///< Get creation index at the start of \b cast insertion
        public uint getCastPhaseIndex() => cast_phase_index;

        ///< Get creation index at the start of HighVariable creation
        public uint getHighLevelIndex() => high_level_index;

        ///< Start \e clean-up phase
        public void startCleanUp()
        {
            clean_up_index = vbank.getCreateIndex();
        }

        ///< Get creation index at the start of \b clean-up phase
        public uint getCleanUpIndex() => clean_up_index;

        /// \brief Generate raw p-code for the function
        ///
        /// Follow flow from the entry point generating PcodeOps for each instruction encountered.
        /// The caller can provide a bounding range that constrains where control can flow to.
        /// \param baddr is the beginning of the constraining range
        /// \param eaddr is the end of the constraining range
        public void followFlow(Address baddr, Address eaddr)
        {
            if (!obank.empty()) {
                if ((flags & Flags.blocks_generated) == 0)
                    throw new LowlevelError("Function loaded for inlining");
                return; // Already translated
            }

            FlowInfo.FlowFlag fl = glb.flowoptions; // Global flow options
            FlowInfo flow = new FlowInfo(this, obank, bblocks, qlst);
            flow.setRange(baddr, eaddr);
            flow.setFlags(fl);
            flow.setMaximumInstructions(glb.max_instructions);
            flow.generateOps();
            size = flow.getSize();
            // Cannot keep track of function sizes in general because of non-contiguous functions
            //  glb.symboltab.update_size(name,size);

            flow.generateBlocks();
            flags |= Flags.blocks_generated;
            switchOverJumpTables(flow);
            if (flow.hasUnimplemented())
                flags |= Flags.unimplemented_present;
            if (flow.hasBadData())
                flags |= Flags.baddata_present;
        }

        /// \brief Generate a clone with truncated control-flow given a partial function
        ///
        /// Existing p-code is cloned from another function whose flow has not been completely
        /// followed. Artificial halt operators are inserted wherever flow is incomplete and
        /// basic blocks are generated.
        /// \param fd is the partial function to clone
        /// \param flow is partial function's flow information
        public void truncatedFlow(Funcdata fd, FlowInfo flow)
        {
            if (!obank.empty())
                throw new LowlevelError("Trying to do truncated flow on pre-existing pcode");

            IEnumerator<PcodeOp> oiter = fd.obank.beginDead(); // Clone the raw pcode
            while (oiter.MoveNext())
                cloneOp(oiter.Current, oiter.Current.getSeqNum());
            obank.setUniqId(fd.obank.getUniqId());

            // Clone callspecs
            for (int i = 0; i < fd.qlst.Count; ++i) {
                FuncCallSpecs oldspec = fd.qlst[i];
                PcodeOp newop = findOp(oldspec.getOp().getSeqNum());
                FuncCallSpecs newspec = oldspec.clone(newop);
                Varnode invn0 = newop.getIn(0);
                if (invn0.getSpace().getType() == spacetype.IPTR_FSPEC) {
                    // Replace embedded pointer to callspec
                    Varnode newvn0 = newVarnodeCallSpecs(newspec);
                    opSetInput(newop, newvn0, 0);
                    deleteVarnode(invn0);
                }
                qlst.Add(newspec);
            }

            IEnumerator<JumpTable> jiter = fd.jumpvec.GetEnumerator(); // Clone the jumptables
            while (jiter.MoveNext()) {
                PcodeOp? indop = jiter.Current.getIndirectOp();
                if (indop == (PcodeOp)null)
                    // If indirect op has not been linked, this is probably a jumptable override
                    // that has not been reached by the flow yet, so we ignore/truncate it
                    continue;
                PcodeOp? newop = findOp(indop.getSeqNum());
                if (newop == (PcodeOp)null)
                    throw new LowlevelError("Could not trace jumptable across partial clone");
                JumpTable jtclone = new JumpTable(jiter.Current);
                jtclone.setIndirectOp(newop);
                jumpvec.Add(jtclone);
            }

            FlowInfo partialflow = new FlowInfo(this,obank,bblocks,qlst,flow); // Clone the flow
            if (partialflow.hasInject())
                partialflow.injectPcode();
            // Clear error reporting flags
            // Keep possible unreachable flag
            partialflow.clearFlags(~FlowInfo.FlowFlag.possible_unreachable);

            partialflow.generateBlocks(); // Generate basic blocks for partial flow
            flags |= Flags.blocks_generated;
        }

        /// \brief In-line the p-code from another function into \b this function
        ///
        /// Raw PcodeOps for the in-line function are generated and then cloned into
        /// \b this function.  Depending on the control-flow complexity of the in-line
        /// function, the PcodeOps are injected as if they are all part of the call site
        /// address (EZModel), or the PcodeOps preserve their address and extra branch
        /// instructions are inserted to integrate control-flow of the in-line into
        /// the calling function.
        /// \param inlinefd is the function to in-line
        /// \param flow is the flow object being injected
        /// \param callop is the site of the injection
        /// \return \b true if the injection was successful
        public bool inlineFlow(Funcdata inlinefd, FlowInfo flow, PcodeOp callop)
        {
            inlinefd.getArch().clearAnalysis(inlinefd);
            FlowInfo inlineflow = new FlowInfo(inlinefd,inlinefd.obank,inlinefd.bblocks,inlinefd.qlst);
            inlinefd.obank.setUniqId(obank.getUniqId());

            // Generate the pcode ops to be inlined
            Address baddr = new Address(baseaddr.getSpace(),0);
            Address eaddr = new Address(baseaddr.getSpace(),ulong.MaxValue);
            inlineflow.setRange(baddr, eaddr);
            inlineflow.setFlags(FlowInfo.FlowFlag.error_outofbounds | FlowInfo.FlowFlag.error_unimplemented |
                FlowInfo.FlowFlag.error_reinterpreted | FlowInfo.FlowFlag.flow_forinline);
            inlineflow.forwardRecursion(flow);
            inlineflow.generateOps();

            if (inlineflow.checkEZModel()) {
                // With an EZ clone there are no jumptables to clone
                IEnumerator<PcodeOp> oiter = obank.endDead();
                // There is at least one op
                --oiter;
                flow.inlineEZClone(inlineflow, callop.getAddr());
                ++oiter;
                if (oiter != obank.endDead()) {
                    // If there was at least one PcodeOp cloned
                    PcodeOp firstop = oiter.Current;
                    oiter = obank.endDead();
                    --oiter;
                    PcodeOp lastop = oiter.Current;
                    obank.moveSequenceDead(firstop, lastop, callop); // Move cloned sequence to right after callop
                    if (callop.isBlockStart())
                        firstop.setFlag(PcodeOp.Flags.startbasic); // First op of inline inherits callop's startbasic flag
                    else
                        firstop.clearFlag(PcodeOp.Flags.startbasic);
                }
                opDestroyRaw(callop);
            }
            else {
                Address retaddr = new Address();
                if (!flow.testHardInlineRestrictions(inlinefd, callop, retaddr))
                    return false;
                // Clone any jumptables from inline piece
                IEnumerator<JumpTable> jiter = inlinefd.jumpvec.GetEnumerator();
                while (jiter.MoveNext()) {
                    JumpTable jtclone = new JumpTable(jiter.Current);
                    jumpvec.Add(jtclone);
                }
                flow.inlineClone(inlineflow, retaddr);

                // Convert CALL op to a jump
                while (callop.numInput() > 1)
                    opRemoveInput(callop, callop.numInput() - 1);

                opSetOpcode(callop, OpCode.CPUI_BRANCH);
                Varnode inlineaddr = newCodeRef(inlinefd.getAddress());
                opSetInput(callop, inlineaddr, 0);
            }
            obank.setUniqId(inlinefd.obank.getUniqId());
            return true;
        }

        /// \brief Override the control-flow p-code for a particular instruction
        ///
        /// P-code in \b this function is modified to change the control-flow of
        /// the instruction at the given address, based on the Override type.
        /// \param addr is the given address of the instruction to modify
        /// \param type is the Override type
        public void overrideFlow(Address addr, Override.Branching type)
        {
            PcodeOpTree::const_iterator iter = beginOp(addr);
            PcodeOpTree::const_iterator enditer = endOp(addr);

            PcodeOp? op = (PcodeOp)null;
            if (type == Override.Branching.BRANCH)
                op = findPrimaryBranch(iter, enditer, false, true, true);
            else if (type == Override.Branching.CALL)
                op = findPrimaryBranch(iter, enditer, true, false, true);
            else if (type == Override.Branching.CALL_RETURN)
                op = findPrimaryBranch(iter, enditer, true, true, true);
            else if (type == Override.Branching.RETURN)
                op = findPrimaryBranch(iter, enditer, true, true, false);

            if ((op == (PcodeOp)null) || (!op.isDead()))
                throw new LowlevelError("Could not apply flowoverride");

            OpCode opc = op.code();
            if (type == Override.Branching.BRANCH) {
                if (opc == OpCode.CPUI_CALL)
                    opSetOpcode(op, OpCode.CPUI_BRANCH);
                else if (opc == OpCode.CPUI_CALLIND)
                    opSetOpcode(op, OpCode.CPUI_BRANCHIND);
                else if (opc == OpCode.CPUI_RETURN)
                    opSetOpcode(op, OpCode.CPUI_BRANCHIND);
            }
            else if ((type == Override.Branching.CALL) || (type == Override.Branching.CALL_RETURN)) {
                if (opc == OpCode.CPUI_BRANCH)
                    opSetOpcode(op, OpCode.CPUI_CALL);
                else if (opc == OpCode.CPUI_BRANCHIND)
                    opSetOpcode(op, OpCode.CPUI_CALLIND);
                else if (opc == OpCode.CPUI_CBRANCH)
                    throw new LowlevelError("Do not currently support CBRANCH overrides");
                else if (opc == OpCode.CPUI_RETURN)
                    opSetOpcode(op, OpCode.CPUI_CALLIND);
                if (type == Override.Branching.CALL_RETURN) {
                    // Insert a new return op after call
                    PcodeOp newReturn = newOp(1, addr);
                    opSetOpcode(newReturn, OpCode.CPUI_RETURN);
                    opSetInput(newReturn, newConstant(1, 0), 0);
                    opDeadInsertAfter(newReturn, op);
                }
            }
            else if (type == Override.Branching.RETURN) {
                if ((opc == OpCode.CPUI_BRANCH) || (opc == OpCode.CPUI_CBRANCH) || (opc == OpCode.CPUI_CALL))
                    throw new LowlevelError("Do not currently support complex overrides");
                else if (opc == OpCode.CPUI_BRANCHIND)
                    opSetOpcode(op, OpCode.CPUI_RETURN);
                else if (opc == OpCode.CPUI_CALLIND)
                    opSetOpcode(op, OpCode.CPUI_RETURN);
            }
        }

        /// \brief Inject p-code from a \e payload into \b this live function
        ///
        /// Raw PcodeOps are generated from the payload within a given basic block at a specific
        /// position in \b this function.
        /// \param payload is the injection payload
        /// \param addr is the address at the point of injection
        /// \param bl is the given basic block holding the new ops
        /// \param iter indicates the point of insertion
        public void doLiveInject(InjectPayload payload, Address addr, BlockBasic bl, IEnumerator<PcodeOp> pos)
        {
            PcodeEmitFd emitter = new PcodeEmitFd();
            InjectContext context = glb.pcodeinjectlib.getCachedContext();

            emitter.setFuncdata(this);
            context.clear();
            context.baseaddr = addr;        // Shouldn't be using inst_next, inst_next2 or inst_start here
            context.nextaddr = addr;

            IEnumerator<PcodeOp> deaditer = obank.endDead();
            bool deadempty = (obank.beginDead() == deaditer);
            if (!deadempty)
                --deaditer;
            payload.inject(context, emitter);
            // Calculate iterator to first injected op
            if (deadempty)
                deaditer = obank.beginDead();
            else
                ++deaditer;
            while (deaditer != obank.endDead()) {
                PcodeOp op = deaditer.Current;
                ++deaditer;
                if (op.isCallOrBranch())
                    throw new LowlevelError("Illegal branching injection");
                opInsert(op, bl, iter);
            }
        }

        /// Print raw p-code op descriptions to a stream
        /// A representation of all PcodeOps in the function body are printed to the
        /// stream. Depending on the state of analysis, PcodeOps are grouped into their
        /// basic blocks, and within a block, ops are displayed sequentially. Basic labeling
        /// of branch destinations is also printed.  This is suitable for a console mode or
        /// debug view of the state of the function at any given point in its analysis.
        /// \param s is the output stream
        public void printRaw(TextWriter s)
        {
            if (bblocks.getSize() == 0) {
                if (obank.empty())
                    throw new RecovError("No operations to print");
                PcodeOpTree::const_iterator iter;
                s.WriteLine("Raw operations: ");
                for (iter = obank.beginAll(); iter != obank.endAll(); ++iter) {
                    s << (*iter).second.getSeqNum() << ":\t";
                    (*iter).second.printRaw(s);
                    s.WriteLine();
                }
            }
            else
                bblocks.printRaw(s);
        }

        /// Print a description of all Varnodes to a stream
        /// A description of each Varnode currently involved in the data-flow of \b this
        /// function is printed to the output stream.  This is suitable as part of a console mode
        /// or debug view of the function at any point during its analysis
        /// \param s is the output stream
        public void printVarnodeTree(TextWriter s)
        {
            VarnodeDefSet::const_iterator iter, enditer;

            iter = vbank.beginDef();
            enditer = vbank.endDef();
            while (iter != enditer) {
                Varnode vn = *iter++;
                vn.printInfo(s);
            }
        }

        /// Print a description of control-flow structuring to a stream
        /// A description of each block in the current structure hierarchy is
        /// printed to stream.  This is suitable for a console mode or debug view
        /// of the state of control-flow structuring at any point during analysis.
        /// \param s is the output stream
        public void printBlockTree(TextWriter s)
        {
            if (sblocks.getSize() != 0)
                sblocks.printTree(s, 0);
        }

        /// Print description of memory ranges associated with local scopes
        /// Each scope has a set of memory ranges associated with it, encompassing
        /// storage locations of variables that are \e assumed to be in the scope.
        /// Each range for each local scope is printed.
        /// \param s is the output stream
        public void printLocalRange(TextWriter s)
        {
            localmap.printBounds(s);
            ScopeMap.Enumerator iter = localmap.childrenBegin();
            while(iter.MoveNext()) {
                Scope l1 = iter.Current.Value;
                l1.printBounds(s);
            }
        }

        /// Encode a description of \b this function to stream
        /// A description of \b this function is written to the stream,
        /// including name, address, prototype, symbol, jump-table, and override information.
        /// If indicated by the caller, a description of the entire PcodeOp and Varnode
        /// tree is also emitted.
        /// \param encoder is the stream encoder
        /// \param id is the unique id associated with the function symbol
        /// \param savetree is \b true if the p-code tree should be emitted
        public void encode(Sla.CORE.Encoder encoder, ulong id, bool savetree)
        {
            encoder.openElement(ElementId.ELEM_FUNCTION);
            if (id != 0)
                encoder.writeUnsignedInteger(AttributeId.ATTRIB_ID, id);
            encoder.writeString(AttributeId.ATTRIB_NAME, name);
            encoder.writeSignedInteger(AttributeId.ATTRIB_SIZE, size);
            if (hasNoCode())
                encoder.writeBool(AttributeId.ATTRIB_NOCODE, true);
            baseaddr.encode(encoder);

            if (!hasNoCode())
            {
                localmap.encodeRecursive(encoder, false);  // Save scope and all subscopes
            }

            if (savetree)
            {
                encodeTree(encoder);
                encodeHigh(encoder);
            }
            encodeJumpTable(encoder);
            funcp.encode(encoder);      // Must be saved after database
            localoverride.encode(encoder, glb);
            encoder.closeElement(ElementId.ELEM_FUNCTION);
        }

        /// Restore the state of \b this function from an XML description
        /// Parse a \<function> element, recovering the name, address, prototype, symbol,
        /// jump-table, and override information for \b this function.
        /// \param decoder is the stream decoder
        /// \return the symbol id associated with the function
        public ulong decode(Sla.CORE.Decoder decoder)
        {
            //  clear();  // Shouldn't be needed
            name = string.Empty;
            size = -1;
            ulong id = 0;
            AddrSpace stackid = glb.getStackSpace();
            uint elemId = decoder.openElement(ElementId.ELEM_FUNCTION);
            while(true) {
                AttributeId attribId = decoder.getNextAttributeId();
                if (attribId == 0) break;
                if (attribId == AttributeId.ATTRIB_NAME)
                    name = decoder.readString();
                else if (attribId == AttributeId.ATTRIB_SIZE) {
                    size = (int)decoder.readSignedInteger();
                }
                else if (attribId == AttributeId.ATTRIB_ID) {
                    id = decoder.readUnsignedInteger();
                }
                else if (attribId == AttributeId.ATTRIB_NOCODE) {
                    if (decoder.readBool())
                        flags |= Flags.no_code;
                }
                else if (attribId == AttributeId.ATTRIB_LABEL)
                    displayName = decoder.readString();
            }
            if (name.Length == 0)
                throw new LowlevelError("Missing function name");
            if (displayName.Length == 0)
                displayName = name;
            if (size == -1)
                throw new LowlevelError("Missing function size");
            baseaddr = Address.decode(decoder);
            while(true) {
                uint subId = decoder.peekElement();
                if (subId == 0) break;
                if (subId == ElementId.ELEM_LOCALDB) {
                    if (localmap != (ScopeLocal)null)
                        throw new LowlevelError("Pre-existing local scope when restoring: " + name);
                    ScopeLocal newMap = new ScopeLocal(id, stackid, this, glb);
                    glb.symboltab.decodeScope(decoder, newMap);   // May delete newMap and throw
                    localmap = newMap;
                }
                else if (subId == ElementId.ELEM_OVERRIDE)
                    localoverride.decode(decoder, glb);
                else if (subId == ElementId.ELEM_PROTOTYPE) {
                    if (localmap == (ScopeLocal)null) {
                        // If we haven't seen a <localdb> tag yet, assume we have a default local scope
                        ScopeLocal newMap = new ScopeLocal(id, stackid, this, glb);
                        Scope scope = glb.symboltab.getGlobalScope();
                        glb.symboltab.attachScope(newMap, scope); // May delete newMap and throw
                        localmap = newMap;
                    }
                    funcp.setScope(localmap, baseaddr + -1); // localmap built earlier
                    funcp.decode(decoder, glb);
                }
                else if (subId == ElementId.ELEM_JUMPTABLELIST)
                    decodeJumpTable(decoder);
            }
            decoder.closeElement(elemId);
            if (localmap == (ScopeLocal)null) {
                // Seen neither <localdb> or <prototype>
                // This is a function shell, so we provide default locals
                ScopeLocal newMap = new ScopeLocal(id, stackid, this, glb);
                Scope scope = glb.symboltab.getGlobalScope();
                glb.symboltab.attachScope(newMap, scope);     // May delete newMap and throw
                localmap = newMap;
                funcp.setScope(localmap, baseaddr + -1);
            }
            localmap.resetLocalWindow();
            return id;
        }

        /// Encode a description of jump-tables to stream
        /// A \<jumptablelist> element is written with \<jumptable> children describing
        /// each jump-table associated with the control-flow of \b this function.
        /// \param encoder is the stream encoder
        public void encodeJumpTable(Sla.CORE.Encoder encoder)
        {
            if (0 == jumpvec.Count) return;
            encoder.openElement(ElementId.ELEM_JUMPTABLELIST);
            IEnumerator<JumpTable> iter = jumpvec.GetEnumerator();
            while (iter.MoveNext())
                iter.Current.encode(encoder);
            encoder.closeElement(ElementId.ELEM_JUMPTABLELIST);
        }

        /// Decode jump-tables from a stream
        /// Parse a \<jumptablelist> element and build a JumpTable object for
        /// each \<jumptable> child element.
        /// \param decoder is the stream decoder
        public void decodeJumpTable(Sla.CORE.Decoder decoder)
        {
            uint elemId = decoder.openElement(ElementId.ELEM_JUMPTABLELIST);
            while (decoder.peekElement() != 0) {
                JumpTable jt = new JumpTable(glb);
                jt.decode(decoder);
                jumpvec.Add(jt);
            }
            decoder.closeElement(elemId);
        }

        /// Encode a description of the p-code tree to stream
        /// A single \<ast> element is produced with children describing Varnodes, PcodeOps, and
        /// basic blocks making up \b this function's current syntax tree.
        /// \param encoder is the stream encoder
        public void encodeTree(Sla.CORE.Encoder encoder)
        {
            encoder.openElement(ElementId.ELEM_AST);
            encoder.openElement(ElementId.ELEM_VARNODES);
            for (int i = 0; i < glb.numSpaces(); ++i) {
                AddrSpace? @base = glb.getSpace(i);
                if (@base == (AddrSpace)null || @base.getType() == spacetype.IPTR_IOP) continue;
                VarnodeLocSet::const_iterator iter = vbank.beginLoc(@base);
                VarnodeLocSet::const_iterator enditer = vbank.endLoc(@base);
                encodeVarnode(encoder, iter, enditer);
            }
            encoder.closeElement(ElementId.ELEM_VARNODES);

            PcodeOp op;
            BlockBasic bs;
            for (int i = 0; i < bblocks.getSize(); ++i) {
                bs = (BlockBasic)bblocks.getBlock(i);
                encoder.openElement(ElementId.ELEM_BLOCK);
                encoder.writeSignedInteger(AttributeId.ATTRIB_INDEX, bs.getIndex());
                bs.encodeBody(encoder);
                IEnumerator<PcodeOp> oiter = bs.beginOp();
                while (oiter.MoveNext()) {
                    op = oiter.Current;
                    op.encode(encoder);
                }
                encoder.closeElement(ElementId.ELEM_BLOCK);
            }
            for (int i = 0; i < bblocks.getSize(); ++i) {
                bs = (BlockBasic)bblocks.getBlock(i);
                if (bs.sizeIn() == 0) continue;
                encoder.openElement(ElementId.ELEM_BLOCKEDGE);
                encoder.writeSignedInteger(AttributeId.ATTRIB_INDEX, bs.getIndex());
                bs.encodeEdges(encoder);
                encoder.closeElement(ElementId.ELEM_BLOCKEDGE);
            }
            encoder.closeElement(ElementId.ELEM_AST);
        }

        /// Encode a description of all HighVariables to stream
        /// This produces a single \<highlist> element, with a \<high> child for each
        /// high-level variable (HighVariable) currently associated with \b this function.
        /// \param encoder is the stream encoder
        public void encodeHigh(Sla.CORE.Encoder encoder)
        {
            Varnode vn;
            HighVariable high;

            if (!isHighOn()) return;
            encoder.openElement(ElementId.ELEM_HIGHLIST);
            VarnodeLocSet::const_iterator iter;
            for (iter = beginLoc(); iter != endLoc(); ++iter) {
                vn = *iter;
                if (vn.isAnnotation()) continue;
                high = vn.getHigh();
                if (high.isMark()) continue;
                high.setMark();
                high.encode(encoder);
            }
            for (iter = beginLoc(); iter != endLoc(); ++iter) {
                vn = *iter;
                if (!vn.isAnnotation())
                    vn.getHigh().clearMark();
            }
            encoder.closeElement(ElementId.ELEM_HIGHLIST);
        }

        ///< Get the Override object for \b this function
        public Override getOverride() => localoverride;

        /// \brief Toggle whether analysis needs to be restarted for \b this function
        /// \param val is \b true if a reset is required
        public void setRestartPending(bool val)
        {
            flags = val ? (flags | Flags.restart_pending) : (flags & ~(Flags.restart_pending));
        }

        /// \brief Does \b this function need to restart its analysis
        /// \return \b true if analysis should be restarted
        public bool hasRestartPending() => ((flags & Flags.restart_pending) != 0);

        /// \brief Does \b this function have instructions marked as \e unimplemented
        /// \return \b true if the function's body contains at least one unimplemented instruction
        public bool hasUnimplemented() => ((flags & Flags.unimplemented_present) != 0);

        ///< Does \b this function flow into bad data
        public bool hasBadData() => ((flags & Flags.baddata_present) != 0);

        /// Mark registers that map to a virtual address space
        /// This routine searches for an marks Varnode objects, like stack-pointer registers,
        /// that are used as a base address for a virtual address space. Each Varnode gets a
        /// special data-type and is marked so that Varnode::isSpacebase() returns \b true.
        public void spacebase()
        {
            VarnodeLocSet::const_iterator iter, enditer;
            int i, numspace;
            Varnode vn;

            for (int j = 0; j < glb.numSpaces(); ++j) {
                AddrSpace? spc = glb.getSpace(j);
                if (spc == (AddrSpace)null) continue;
                numspace = spc.numSpacebase();
                for (i = 0; i < numspace; ++i) {
                    VarnodeData point = spc.getSpacebase(i);
                    // Find input varnode at this size and location
                    Datatype ct = glb.types.getTypeSpacebase(spc, getAddress());
                    Datatype ptr = glb.types.getTypePointer((int)point.size, ct, spc.getWordSize());

                    iter = vbank.beginLoc((int)point.size, new Address(point.space, point.offset));
                    enditer = vbank.endLoc((int)point.size, new Address(point.space, point.offset));
                    while (iter != enditer) {
                        vn = *iter++;
                        if (vn.isFree()) continue;
                        if (vn.isSpacebase()) {
                            // This has already been marked spacebase
                          // We have given it a chance for descendants to
                          // be eliminated naturally, now force a split if
                          // it still has multiple descendants
                            PcodeOp? op = vn.getDef();
                            if ((op != (PcodeOp)null) && (op.code() == OpCode.CPUI_INT_ADD))
                                splitUses(vn);
                        }
                        else {
                            vn.setFlags(Varnode.varnode_flags.spacebase); // Mark all base registers (not just input)
                            if (vn.isInput())  // Only set type on the input spacebase register
                                vn.updateType(ptr, true, true);
                        }
                    }
                }
            }
        }

        /// Construct a new \e spacebase register for a given address space
        /// Given an address space, like \e stack, that is known to have a base register
        /// pointing to it, construct a Varnode representing that register.
        /// \param id is the \e stack like address space
        /// \return a newly allocated stack-pointer Varnode
        public Varnode newSpacebasePtr(AddrSpace id)
        {
            // Assume that id has a base register (otherwise an exception is thrown)
            VarnodeData point = id.getSpacebase(0);
            Varnode vn = newVarnode((int)point.size, new Address(point.space, point.offset));
            return vn;
        }

        /// Given an address space, like \e stack, that is known to have a base register
        /// pointing to it, try to locate the unique Varnode that holds the input value
        /// of this register.
        /// \param id is the \e stack like address space
        /// \return the input stack-pointer Varnode (or NULL if it doesn't exist)
        public Varnode findSpacebaseInput(AddrSpace id)
        {

            // Assume that id has a base register (otherwise an exception is thrown)
            VarnodeData point = id.getSpacebase(0);
            Varnode vn = vbank.findInput((int)point.size, new Address(point.space, point.offset));
            return vn;
        }

        /// \brief Convert a constant pointer into a \e ram OpCode.CPUI_PTRSUB
        ///
        /// A constant known to be a pointer into an address space like \b ram is converted
        /// into a Varnode defined by OpCode.CPUI_PTRSUB, which triggers a Symbol lookup at points
        /// during analysis.  The constant must point to a known Symbol.
        ///
        /// The PTRSUB takes the constant 0 as its first input, which is marked
        /// as a \e spacebase to indicate this situation. The second input to PTRSUB becomes
        /// the offset to the Symbol within the address space. An additional INT_SUB may be inserted
        /// to get from the start of the Symbol to the address indicated by the original
        /// constant pointer.
        /// \param op is the PcodeOp referencing the constant pointer
        /// \param slot is the input slot of the constant pointer
        /// \param entry is the Symbol being pointed (in)to
        /// \param rampoint is the constant pointer interpreted as an Address
        /// \param origval is the constant
        /// \param origsize is the size of the constant
        public void spacebaseConstant(PcodeOp op, int slot, SymbolEntry entry,
            Address rampoint, ulong origval, int origsize)
        {
            int sz = rampoint.getAddrSize();
            AddrSpace spaceid = rampoint.getSpace();
            Datatype sb_type = glb.types.getTypeSpacebase(spaceid, new Address());
            sb_type = glb.types.getTypePointer(sz, sb_type, spaceid.getWordSize());
            Varnode spacebase_vn;
            Varnode outvn;
            Varnode newconst;

            ulong extra = rampoint.getOffset() - entry.getAddr().getOffset();      // Offset from beginning of entry
            extra = AddrSpace.byteToAddress(extra, rampoint.getSpace().getWordSize());    // Convert to address units

            PcodeOp? addOp = (PcodeOp)null;
            PcodeOp? extraOp = (PcodeOp)null;
            PcodeOp? zextOp = (PcodeOp)null;
            PcodeOp? subOp = (PcodeOp)null;
            bool isCopy = false;
            if (op.code() == OpCode.CPUI_COPY) {
                // We replace COPY with final op of this calculation
                isCopy = true;
                if (sz < origsize)
                    zextOp = op;
                else {
                    op.insertInput(1); // PTRSUB, ADD, SUBPIECE all take 2 parameters
                    if (origsize < sz)
                        subOp = op;
                    else if (extra != 0)
                        extraOp = op;
                    else
                        addOp = op;
                }
            }
            spacebase_vn = newConstant(sz, 0);
            spacebase_vn.updateType(sb_type, true, true);
            spacebase_vn.setFlags(Varnode.varnode_flags.spacebase);
            if (addOp == (PcodeOp)null) {
                addOp = newOp(2, op.getAddr());
                opSetOpcode(addOp, OpCode.CPUI_PTRSUB);
                newUniqueOut(sz, addOp);
                opInsertBefore(addOp, op);
            }
            else {
                opSetOpcode(addOp, OpCode.CPUI_PTRSUB);
            }
            outvn = addOp.getOut();
            // Make sure newconstant and extra preserve origval in address units
            ulong newconstoff = origval - extra;        // everything is already in address units
            newconst = newConstant(sz, newconstoff);
            newconst.setPtrCheck();    // No longer need to check this constant as a pointer
            if (spaceid.isTruncated())
                addOp.setPtrFlow();
            opSetInput(addOp, spacebase_vn, 0);
            opSetInput(addOp, newconst, 1);

            Symbol sym = entry.getSymbol();
            Datatype entrytype = sym.getType();
            Datatype ptrentrytype = glb.types.getTypePointerStripArray(sz, entrytype, spaceid.getWordSize());
            bool typelock = sym.isTypeLocked();
            if (typelock && (entrytype.getMetatype() == type_metatype.TYPE_UNKNOWN))
                typelock = false;
            outvn.updateType(ptrentrytype, typelock, false);
            if (extra != 0) {
                if (extraOp == (PcodeOp)null) {
                    extraOp = newOp(2, op.getAddr());
                    opSetOpcode(extraOp, OpCode.CPUI_INT_ADD);
                    newUniqueOut(sz, extraOp);
                    opInsertBefore(extraOp, op);
                }
                else
                    opSetOpcode(extraOp, OpCode.CPUI_INT_ADD);
                Varnode extconst = newConstant(sz, extra);
                extconst.setPtrCheck();
                opSetInput(extraOp, outvn, 0);
                opSetInput(extraOp, extconst, 1);
                outvn = extraOp.getOut();
            }
            if (sz < origsize) {
                // The new constant is smaller than the original varnode, so we extend it
                if (zextOp == (PcodeOp)null) {
                    zextOp = newOp(1, op.getAddr());
                    opSetOpcode(zextOp, OpCode.CPUI_INT_ZEXT); // Create an extension to get back to original varnode size
                    newUniqueOut(origsize, zextOp);
                    opInsertBefore(zextOp, op);
                }
                else
                    opSetOpcode(zextOp, OpCode.CPUI_INT_ZEXT);
                opSetInput(zextOp, outvn, 0);
                outvn = zextOp.getOut();
            }
            else if (origsize < sz) {
                // The new constant is bigger than the original varnode, truncate it
                if (subOp == (PcodeOp)null) {
                    subOp = newOp(2, op.getAddr());
                    opSetOpcode(subOp, OpCode.CPUI_SUBPIECE);
                    newUniqueOut(origsize, subOp);
                    opInsertBefore(subOp, op);
                }
                else
                    opSetOpcode(subOp, OpCode.CPUI_SUBPIECE);
                opSetInput(subOp, outvn, 0);
                opSetInput(subOp, newConstant(4, 0), 1);    // Take least significant piece
                outvn = subOp.getOut();
            }
            if (!isCopy)
                opSetInput(op, outvn, slot);
        }

        public int getHeritagePass() => heritage.getPass(); ///< Get overall count of heritage passes

        /// \brief Get the number of heritage passes performed for the given address space
        /// \param spc is the address space
        /// \return the number of passes performed
        public int numHeritagePasses(AddrSpace spc) => heritage.numHeritagePasses(spc);

        /// \brief Mark that dead Varnodes have been seen in a specific address space
        /// \param spc is the address space to mark
        public void seenDeadcode(AddrSpace spc)
        {
            heritage.seenDeadCode(spc);
        }

        /// \brief Set a delay before removing dead code for a specific address space
        /// \param spc is the specific address space
        /// \param delay is the number of passes to delay
        public void setDeadCodeDelay(AddrSpace spc, int delay)
        {
            heritage.setDeadCodeDelay(spc, delay);
        }

        /// \brief Check if dead code removal is allowed for a specific address space
        /// \param spc is the specific address space
        /// \return \b true if dead code removal is allowed
        public bool deadRemovalAllowed(AddrSpace spc) => heritage.deadRemovalAllowed(spc);

        /// \brief Check if dead Varnodes have been removed for a specific address space
        /// \param spc is the specific address space
        /// \return \b true if dead code removal has happened in the space
        public bool deadRemovalAllowedSeen(AddrSpace spc) => heritage.deadRemovalAllowedSeen(spc);

        /// \brief Check if a specific Varnode has been linked in fully to the syntax tree (SSA)
        /// \param vn is the specific Varnode
        /// \return \b true if the Varnode is fully linked
        public bool isHeritaged(Varnode vn) => (heritage.heritagePass(vn.getAddr()) >= 0);

        ///< Get the list of guarded LOADs
        public List<LoadGuard> getLoadGuards() => heritage.getLoadGuards();

        ///< Get the list of guarded STOREs
        public List<LoadGuard> getStoreGuards() => heritage.getStoreGuards();

        ///< Get LoadGuard associated with STORE op
        public LoadGuard? getStoreGuard(PcodeOp op) => heritage.getStoreGuard(op);

        // Function prototype and call specification routines
        /// Get the number of calls made by \b this function
        public int numCalls() => qlst.size();

        /// Get the i-th call specification
        public FuncCallSpecs getCallSpecs(int i) => qlst[i];

        /// Get the call specification associated with a CALL op
        public FuncCallSpecs getCallSpecs(PcodeOp op)
        {
            int i;
            Varnode vn = op.getIn(0);
            if (vn.getSpace().getType() == spacetype.IPTR_FSPEC)
                return FuncCallSpecs.getFspecFromConst(vn.getAddr());

            for (i = 0; i < qlst.size(); ++i)
                if (qlst[i].getOp() == op) return qlst[i];
            return (FuncCallSpecs)null;
        }

        /// Recover and return the \e extrapop for this function
        /// If \e extrapop is unknown, recover it from what we know about this function
        /// and set the value permanently for \b this Funcdata object.
        /// If there is no function body it may be impossible to know the value, in which case
        /// this returns the reserved value indicating \e extrapop is unknown.
        ///
        /// \return the recovered value
        public int fillinExtrapop()
        {
            if (hasNoCode())        // If no code to make a decision on
                return funcp.getExtraPop(); // either we already know it or we don't

            if (funcp.getExtraPop() != ProtoModel.extrapop_unknown)
                return funcp.getExtraPop(); // If we already know it, just return it

            IEnumerator<PcodeOp> iter = beginOp(OpCode.CPUI_RETURN);
            if (!iter.MoveNext()) return 0; // If no return statements, answer is irrelevant

            PcodeOp retop = iter.Current;
            byte[] buffer = new byte[4];
            glb.loader.loadFill(buffer, 4, retop.getAddr());

            // We are assuming x86 code here
            int extrapop = 4;      // The default case
            if (buffer[0] == 0xc2) {
                extrapop = buffer[2];   // Pull out immediate 16-bits
                extrapop <<= 8;
                extrapop += buffer[1];
                extrapop += 4;      // extra 4 for the return address
            }
            funcp.setExtraPop(extrapop); // Save what we have learned on the prototype
            return extrapop;

        }

        // Varnode routines
        /// Get the total number of Varnodes
        public int numVarnodes() => vbank.numVarnodes();

        /// Create a new output Varnode
        /// Create a new Varnode which is already defined as output of a given PcodeOp.
        /// This if more efficient as it avoids the initial insertion of the free form of the
        /// Varnode into the tree, and queryProperties only needs to be called once.
        /// \param s is the size of the new Varnode in bytes
        /// \param m is the storage Address of the Varnode
        /// \param op is the given PcodeOp whose output is created
        /// \return the new Varnode object
        public Varnode newVarnodeOut(int s, Address m, PcodeOp op)
        {
            Datatype ct = glb.types.getBase(s, type_metatype.TYPE_UNKNOWN);
            Varnode vn = vbank.createDef(s, m, ct, op);
            op.setOutput(vn);
            assignHigh(vn);

            if (s >= minLanedSize)
                checkForLanedRegister(s, m);
            Varnode.varnode_flags vflags = 0;
            SymbolEntry? entry = localmap.queryProperties(m, s, op.getAddr(), out vflags);
            if (entry != (SymbolEntry)null)
                vn.setSymbolProperties(entry);
            else
                vn.setFlags(vflags & ~Varnode.varnode_flags.typelock); // Typelock set by updateType

            return vn;
        }

        /// Create a new \e temporary output Varnode
        /// Allocate a new register from the \e unique address space and create a new
        /// Varnode object representing it as an output to the given PcodeOp
        /// \param s is the size of the new Varnode in bytes
        /// \param op is the given PcodeOp whose output is created
        /// \return the new temporary register Varnode
        public Varnode newUniqueOut(int s, PcodeOp op)
        {
            Datatype ct = glb.types.getBase(s, type_metatype.TYPE_UNKNOWN);
            Varnode vn = vbank.createDefUnique(s, ct, op);
            op.setOutput(vn);
            assignHigh(vn);
            if (s >= minLanedSize)
                checkForLanedRegister(s, vn.getAddr());
            // No chance of matching localmap
            return vn;
        }

        /// \brief Create a new unattached Varnode object
        ///
        /// \param s is the size of the new Varnode in bytes
        /// \param m is the storage Address of the Varnode
        /// \param ct is a data-type to associate with the Varnode
        /// \return the newly allocated Varnode object
        public Varnode newVarnode(int s, Address m, Datatype? ct = null)
        {
            if (ct == (Datatype)null)
                ct = glb.types.getBase(s, type_metatype.TYPE_UNKNOWN);

            Varnode vn = vbank.create(s, m, ct);
            assignHigh(vn);

            if (s >= minLanedSize)
                checkForLanedRegister(s, m);
            Varnode.varnode_flags vflags = 0;
            SymbolEntry? entry = localmap.queryProperties(vn.getAddr(), vn.getSize(), new Address(), out vflags);
            if (entry != (SymbolEntry)null)   // Let entry try to force type
                vn.setSymbolProperties(entry);
            else
                vn.setFlags(vflags & ~Varnode.varnode_flags.typelock); // Typelock set by updateType

            return vn;
        }

        /// Create a new \e constant Varnode
        /// A Varnode is allocated which represents the indicated constant value.
        /// Its storage address is in the \e constant address space.
        /// \param s is the size of the Varnode in bytes
        /// \param constant_val is the indicated constant value
        /// \return the new Varnode object
        public Varnode newConstant(int s, ulong constant_val)
        {
            Datatype ct = glb.types.getBase(s, type_metatype.TYPE_UNKNOWN);

            Varnode vn = vbank.create(s, glb.getConstant(constant_val), ct);
            assignHigh(vn);
            // There is no chance of matching localmap
            return vn;
        }

        /// Create a new Varnode given an address space and offset
        /// \param s is the size of the Varnode in bytes
        /// \param base is the address space of the Varnode
        /// \param off is the offset into the address space of the Varnode
        /// \return the newly allocated Varnode
        public Varnode newVarnode(int s, AddrSpace @base, ulong off)
        {
            Varnode vn = newVarnode(s, new Address(@base, off));
            return vn;
        }

        /// Create a PcodeOp \e annotation Varnode
        /// Create a special \e annotation Varnode that holds a pointer reference to a specific
        /// PcodeOp.  This is used specifically to let a OpCode.CPUI_INDIRECT op refer to the PcodeOp
        /// it is holding an indirect effect for.
        /// \param op is the PcodeOp to encode in the annotation
        /// \return the newly allocated \e annotation Varnode
        public Varnode newVarnodeIop(PcodeOp op)
        {
            Datatype ct = glb.types.getBase(sizeof(op), type_metatype.TYPE_UNKNOWN);
            AddrSpace cspc = glb.getIopSpace();
            Varnode vn = vbank.create(sizeof(op), new Address(cspc, (ulong)(ulong)op), ct);
            assignHigh(vn);
            return vn;
        }

        /// Create a constant Varnode referring to an address space
        /// A reference to a particular address space is encoded as a constant Varnode.
        /// These are used for LOAD and STORE p-code ops in particular.
        /// \param spc is the address space to encode
        /// \return the newly allocated constant Varnode
        public Varnode newVarnodeSpace(AddrSpace spc)
        {
            Datatype ct = glb.types.getBase(sizeof(spc), type_metatype.TYPE_UNKNOWN);
            Varnode vn = vbank.create(sizeof(spc), glb.createConstFromSpace(spc), ct);
            assignHigh(vn);
            return vn;
        }

        /// Create a call specification \e annotation Varnode
        /// A call specification (FuncCallSpecs) is encoded into an \e annotation Varnode.
        /// The Varnode is used specifically as an input to OpCode.CPUI_CALL ops to speed up access
        /// to their associated call specification.
        /// \param fc is the call specification to encode
        /// \return the newly allocated \e annotation Varnode
        public Varnode newVarnodeCallSpecs(FuncCallSpecs fc)
        {
            Datatype ct = glb.types.getBase(sizeof(fc), type_metatype.TYPE_UNKNOWN);
            AddrSpace cspc = glb.getFspecSpace();
            Varnode vn = vbank.create(sizeof(fc), new Address(cspc, (ulong)(ulong)fc), ct);
            assignHigh(vn);
            return vn;
        }

        /// Create a new \e temporary Varnode
        /// A new temporary register storage location is allocated from the \e unique
        /// address space
        /// \param s is the size of the Varnode in bytes
        /// \param ct is an optional data-type to associated with the Varnode
        /// \return the newly allocated \e temporary Varnode
        public Varnode newUnique(int s, Datatype ct = null)
        {
            if (ct == (Datatype)null)
                ct = glb.types.getBase(s, type_metatype.TYPE_UNKNOWN);
            Varnode vn = vbank.createUnique(s, ct);
            assignHigh(vn);
            if (s >= minLanedSize)
                checkForLanedRegister(s, vn.getAddr());

            // No chance of matching localmap
            return vn;
        }

        /// Create a code address \e annotation Varnode
        /// A reference to a specific Address is encoded in a Varnode.  The Varnode is
        /// an \e annotation in the sense that it will hold no value in the data-flow, it will
        /// will only hold a reference to an address. This is used specifically by the branch
        /// p-code operations to hold destination addresses.
        /// \param m is the Address to encode
        /// \return the newly allocated \e annotation Varnode
        public Varnode newCodeRef(Address m)
        {
            Datatype ct = glb.types.getTypeCode();
            Varnode vn = vbank.create(1, m, ct);
            vn.setFlags(Varnode.varnode_flags.annotation);
            assignHigh(vn);
            return vn;
        }

        /// Mark a Varnode as an input to the function
        /// An \e input Varnode has a special designation within SSA form as not being defined
        /// by a p-code operation and is a formal input to the data-flow of the function.  It is
        /// not necessarily a formal function parameter.
        ///
        /// The given Varnode to be marked is also returned unless there is an input Varnode that
        /// already exists which overlaps the given Varnode.  If the Varnodes have the same size and
        /// storage address, the preexisting input Varnode is returned instead. Otherwise an
        /// exception is thrown.
        /// \param vn is the given Varnode to mark as an input
        /// \return the marked Varnode
        public Varnode setInputVarnode(Varnode vn)
        {
            Varnode invn;

            if (vn.isInput()) return vn;   // Already an input
                                            // First we check if it overlaps any other varnode
            VarnodeDefSet::const_iterator iter;
            iter = vbank.beginDef(Varnode.varnode_flags.input, vn.getAddr() + vn.getSize());

            // Iter points at first varnode AFTER vn
            if (iter != vbank.beginDef()) {
                --iter;         // previous varnode
                invn = iter.Current;       // comes before vn or intersects
                if (invn.isInput()) {
                    if ((-1 != vn.overlap(invn)) || (-1 != invn.overlap(vn))) {
                        if ((vn.getSize() == invn.getSize()) && (vn.getAddr() == invn.getAddr()))
                            return invn;
                        throw new LowlevelError("Overlapping input varnodes");
                    }
                }
            }

            vn = vbank.setInput(vn);
            setVarnodeProperties(vn);
            EffectRecord.EffectType effecttype = funcp.hasEffect(vn.getAddr(), vn.getSize());
            if (effecttype == EffectRecord.EffectType.unaffected)
                vn.setUnaffected();
            if (effecttype == EffectRecord.EffectType.return_address) {
                vn.setUnaffected();    // Should be unaffected over the course of the function
                vn.setReturnAddress();
            }
            return vn;
        }

        /// \brief Adjust input Varnodes contained in the given range
        ///
        /// After this call, a single \e input Varnode will exist that fills the given range.
        /// Any previous input Varnodes contained in this range are redefined using a SUBPIECE
        /// op off of the new single input.  If an overlapping Varnode isn't fully contained
        /// an exception is thrown.
        /// \param addr is the starting address of the range
        /// \param sz is the number of bytes in the range
        public void adjustInputVarnodes(Address addr, int sz)
        {
            Address endaddr = addr + (sz - 1);
            List<Varnode> inlist = new List<Varnode>();
            VarnodeDefSet::const_iterator iter, enditer;
            iter = vbank.beginDef(Varnode.varnode_flags.input, addr);
            enditer = vbank.endDef(Varnode.varnode_flags.input, endaddr);
            while (iter != enditer)
            {
                Varnode vn = iter.Current;
                ++iter;
                if (vn.getOffset() + (uint)(vn.getSize() - 1) > endaddr.getOffset())
                    throw new LowlevelError("Cannot properly adjust input varnodes");
                inlist.Add(vn);
            }

            for (int i = 0; i < inlist.size(); ++i) {
                Varnode vn = inlist[i];
                int sa = addr.justifiedContain(sz, vn.getAddr(), vn.getSize(), false);
                if ((!vn.isInput()) || (sa < 0) || (sz <= vn.getSize()))
                    throw new LowlevelError("Bad adjustment to input varnode");
                PcodeOp subop = newOp(2, getAddress());
                opSetOpcode(subop, OpCode.CPUI_SUBPIECE);
                opSetInput(subop, newConstant(4, (ulong)sa), 1);
                Varnode newvn = newVarnodeOut(vn.getSize(), vn.getAddr(), subop);
                // newvn must not be free in order to give all vn's descendants
                opInsertBegin(subop, (BlockBasic)bblocks.getBlock(0));
                totalReplace(vn, newvn);
                deleteVarnode(vn); // Get rid of old input before creating new input
                inlist[i] = newvn;
            }
            // Now that all the intersecting inputs have been pulled out, we can create the new input
            Varnode invn = newVarnode(sz, addr);
            invn = setInputVarnode(invn);
            // The new input may cause new heritage and "Heritage AFTER dead removal" errors
            // So tell heritage to ignore it
            // FIXME: It would probably be better to insert this directly into heritage's globaldisjoint
            invn.setWriteMask();
            // Now change all old inputs to be created as SUBPIECE from the new input
            for (int i = 0; i < inlist.size(); ++i) {
                PcodeOp op = inlist[i].getDef();
                opSetInput(op, invn, 0);
            }
        }

        /// Delete the given varnode
        public void deleteVarnode(Varnode vn)
        {
            vbank.destroy(vn);
        }

        /// Find range covering given Varnode and any intersecting Varnodes
        /// Find the minimal Address range covering the given Varnode that doesn't split other Varnodes
        /// \param vn is the given Varnode
        /// \param sz is used to pass back the size of the resulting range
        /// \return the starting address of the resulting range
        public Address findDisjointCover(Varnode vn, int sz)
        {
            Address addr = vn.getAddr();
            Address endaddr = addr + vn.getSize();
            VarnodeLocSet::const_iterator iter = vn.lociter;

            while (iter != beginLoc()) {
                --iter;
                Varnode curvn = iter.Current;
                Address curEnd = curvn.getAddr() + curvn.getSize();
                if (curEnd <= addr) break;
                addr = curvn.getAddr();
            }
            iter = vn.lociter;
            while (iter != endLoc()) {
                Varnode curvn = iter;
                ++iter;
                if (endaddr <= curvn.getAddr()) break;
                endaddr = curvn.getAddr() + curvn.getSize();
            }
            sz = (int)(endaddr.getOffset() - addr.getOffset());
            return addr;
        }

        /// \brief Find the first input Varnode covered by the given range
        /// \param s is the size of the range in bytes
        /// \param loc is the starting address of the range
        /// \return the matching Varnode or NULL
        public Varnode findCoveredInput(int s, Address loc) => vbank.findCoveredInput(s, loc);

        /// \brief Find the input Varnode that contains the given range
        /// \param s is the size of the range in bytes
        /// \param loc is the starting address of the range
        /// \return the matching Varnode or NULL
        public Varnode findCoveringInput(int s, Address loc) => vbank.findCoveringInput(s, loc);

        /// \brief Find the input Varnode with the given size and storage address
        /// \param s is the size in bytes
        /// \param loc is the storage address
        /// \return the matching Varnode or NULL
        public Varnode findVarnodeInput(int s, Address loc) => vbank.findInput(s, loc);

        /// \brief Find a defined Varnode via its storage address and its definition address
        /// \param s is the size in bytes
        /// \param loc is the storage address
        /// \param pc is the address where the Varnode is defined
        /// \param uniq is an (optional) sequence number to match
        /// \return the matching Varnode or NULL
        public Varnode findVarnodeWritten(int s, Address loc, Address pc, uint uniq = uint.MaxValue)
            => vbank.find(s, loc, pc, uniq);

        /// \brief Start of all Varnodes sorted by storage
        public VarnodeLocSet::const_iterator beginLoc() => vbank.beginLoc();

        /// \brief End of all Varnodes sorted by storage
        public VarnodeLocSet::const_iterator endLoc() => vbank.endLoc();

        /// \brief Start of Varnodes stored in a given address space
        public VarnodeLocSet::const_iterator beginLoc(AddrSpace spaceid) => vbank.beginLoc(spaceid);

        /// \brief End of Varnodes stored in a given address space
        public VarnodeLocSet::const_iterator endLoc(AddrSpace spaceid) => vbank.endLoc(spaceid);

        /// \brief Start of Varnodes at a storage address
        public VarnodeLocSet::const_iterator beginLoc(Address addr) => vbank.beginLoc(addr);

        /// \brief End of Varnodes at a storage address
        public VarnodeLocSet::const_iterator endLoc(Address addr) => vbank.endLoc(addr);

        /// \brief Start of Varnodes with given storage
        public VarnodeLocSet::const_iterator beginLoc(int s, Address addr) => vbank.beginLoc(s, addr);

        /// \brief End of Varnodes with given storage
        public VarnodeLocSet::const_iterator endLoc(int s, Address addr) => vbank.endLoc(s, addr);

        /// \brief Start of Varnodes matching storage and properties
        public VarnodeLocSet::const_iterator beginLoc(int s, Address addr, Varnode.varnode_flags fl)
            => vbank.beginLoc(s, addr, fl);

        /// \brief End of Varnodes matching storage and properties
        public VarnodeLocSet::const_iterator endLoc(int s, Address addr, Varnode.varnode_flags fl)
            => vbank.endLoc(s, addr, fl);

        /// \brief Start of Varnodes matching storage and definition address
        public VarnodeLocSet::const_iterator beginLoc(int s, Address addr, Address pc, uint uniq = uint.MaxValue)
            => vbank.beginLoc(s, addr, pc, uniq);

        /// \brief End of Varnodes matching storage and definition address
        public VarnodeLocSet::const_iterator endLoc(int s, Address addr, Address pc, uint uniq = uint.MaxValue)
            => vbank.endLoc(s, addr, pc, uniq);

        /// \brief Given start, return maximal range of overlapping Varnodes
        public Varnode.varnode_flags overlapLoc(VarnodeLocSet::const_iterator iter, List<VarnodeLocSet::const_iterator> bounds)
            => vbank.overlapLoc(iter, bounds);

        /// \brief Start of all Varnodes sorted by definition address
        public VarnodeDefSet::const_iterator beginDef() => vbank.beginDef();

        /// \brief End of all Varnodes sorted by definition address
        public VarnodeDefSet::const_iterator endDef() => vbank.endDef();

        /// \brief Start of Varnodes with a given definition property
        public VarnodeDefSet::const_iterator beginDef(Varnode.varnode_flags fl) => vbank.beginDef(fl);

        /// \brief End of Varnodes with a given definition property
        public VarnodeDefSet::const_iterator endDef(Varnode.varnode_flags fl) => vbank.endDef(fl);

        /// \brief Start of (input or free) Varnodes at a given storage address
        public VarnodeDefSet::const_iterator beginDef(Varnode.varnode_flags fl, Address addr)
            => vbank.beginDef(fl, addr);

        /// \brief End of (input or free) Varnodes at a given storage address
        public VarnodeDefSet::const_iterator endDef(Varnode.varnode_flags fl, Address addr)
            => vbank.endDef(fl, addr);

        /// Check for a potential laned register
        /// Check if the given storage range is a potential laned register.
        /// If so, record the storage with the matching laned register record.
        /// \param sz is the size of the storage range in bytes
        /// \param addr is the starting address of the storage range
        public void checkForLanedRegister(int sz, Address addr)
        {
            LanedRegister lanedRegister = glb.getLanedRegister(addr, sz);
            if (lanedRegister == (LanedRegister)null)
                return;
            VarnodeData storage = new VarnodeData();
            storage.space = addr.getSpace();
            storage.offset = addr.getOffset();
            storage.size = (uint)sz;
            lanedMap[storage] = lanedRegister;
        }

        /// Beginning iterator over laned accesses
        public Dictionary<VarnodeData, LanedRegister>.Enumerator beginLaneAccess() => lanedMap.GetEnumerator();

        ///// Ending iterator over laned accesses
        //public Dictionary<VarnodeData, LanedRegister>.Enumerator endLaneAccess() => lanedMap.end();

        /// Clear records from the laned access list
        public void clearLanedAccessMap()
        {
            lanedMap.Clear();
        }

        /// Find a high-level variable by name
        /// Look up the Symbol visible in \b this function's Scope and return the HighVariable
        /// associated with it.  If the Symbol doesn't exist or there is no Varnode holding at least
        /// part of the value of the Symbol, NULL is returned.
        /// \param nm is the name to search for
        /// \return the matching HighVariable or NULL
        public HighVariable findHigh(string nm)
        {
            List<Symbol> symList = new List<Symbol>();
            localmap.queryByName(nm, symList);
            if (symList.empty()) return (HighVariable)null;
            Symbol sym = symList[0];
            Varnode vn = findLinkedVarnode(sym.getFirstWholeMap());
            if (vn != (Varnode)null)
                return vn.getHigh();

            return (HighVariable)null;
        }

        /// Make sure there is a Symbol entry for all global Varnodes
        /// Search for \e addrtied Varnodes whose storage falls in the global Scope, then
        /// build a new global Symbol if one didn't exist before.
        public void mapGlobals()
        {
            SymbolEntry entry;
            VarnodeLocSet::const_iterator iter, enditer;
            Varnode vn, maxvn;
            Datatype ct;
            Varnode.varnode_flags fl;
            List<Varnode> uncoveredVarnodes = new List<Varnode>();
            bool inconsistentuse = false;

            iter = vbank.beginLoc(); // Go through all varnodes for this space
            enditer = vbank.endLoc();
            while (iter != enditer) {
                vn = *iter++;
                if (vn.isFree()) continue;
                if (!vn.isPersist()) continue; // Could be a code ref
                if (vn.getSymbolEntry() != (SymbolEntry)null) continue;
                maxvn = vn;
                Address addr = vn.getAddr();
                Address endaddr = addr + vn.getSize();
                uncoveredVarnodes.Clear();
                while (iter != enditer) {
                    vn = iter.Current;
                    if (!vn.isPersist()) break;
                    if (vn.getAddr() < endaddr) {
                        // Varnodes at the same base address will get linked to the Symbol at that address
                        // even if the size doesn't match, but we check for internal Varnodes that
                        // do not have an attached Symbol as these won't get linked to anything
                        if (vn.getAddr() != addr && vn.getSymbolEntry() == (SymbolEntry)null)
                            uncoveredVarnodes.Add(vn);
                        endaddr = vn.getAddr() + vn.getSize();
                        if (vn.getSize() > maxvn.getSize())
                            maxvn = vn;
                        ++iter;
                    }
                    else
                        break;
                }
                if ((maxvn.getAddr() == addr) && (addr + maxvn.getSize() == endaddr))
                    ct = maxvn.getHigh().getType();
                else
                    ct = glb.types.getBase((int)(endaddr.getOffset() - addr.getOffset()), type_metatype.TYPE_UNKNOWN);
                fl = 0;
                // Assume existing symbol is addrtied, so use empty usepoint
                Address usepoint = new Address();
                // Find any entry overlapping base address
                entry = localmap.queryProperties(addr, 1, usepoint, out fl);
                if (entry == (SymbolEntry)null) {
                    Scope? discover = localmap.discoverScope(addr, ct.getSize(), usepoint);
                    if (discover == (Scope)null)
                        throw new LowlevelError("Could not discover scope");
                    int index = 0;
                    string symbolname = discover.buildVariableName(addr, usepoint, ct, index,
                        Varnode.varnode_flags.addrtied | Varnode.varnode_flags.persist);
                    discover.addSymbol(symbolname, ct, addr, usepoint);
                }
                else if ((addr.getOffset() + (uint)ct.getSize()) - 1 > (entry.getAddr().getOffset() + (uint)entry.getSize()) - 1)
                {
                    inconsistentuse = true;
                    if (!uncoveredVarnodes.empty()) // Provide Symbols for any uncovered internal Varnodes
                        coverVarnodes(entry, uncoveredVarnodes);
                }
            }
            if (inconsistentuse)
                warningHeader("Globals starting with '_' overlap smaller symbols at the same address");
        }

        /// Prepare for recovery of the "this" pointer
        /// Make sure that if a Varnode exists representing the "this" pointer for the function, that it
        /// is treated as pointer data-type.
        public void prepareThisPointer()
        {
            int numInputs = funcp.numParams();
            for (int i = 0; i < numInputs; ++i) {
                ProtoParameter param = funcp.getParam(i);
                if (param.isThisPointer() && param.isTypeLocked())
                    return;     // Data-type will be obtained directly from symbol
            }

            // Its possible that a recommendation for the "this" pointer has already been been collected.
            // Currently the only type recommendations are for the "this" pointer. If there any, it is for "this"
            if (localmap.hasTypeRecommendations())
                return;

            Datatype dt = glb.types.getTypeVoid();
            AddrSpace spc = glb.getDefaultDataSpace();
            dt = glb.types.getTypePointer((int)spc.getAddrSize(), dt, spc.getWordSize());
            Address addr = funcp.getThisPointerStorage(dt);
            localmap.addTypeRecommendation(addr, dt);
        }

        /// \brief Test for legitimate double use of a parameter trial
        ///
        /// The given trial is a \e putative input to first CALL, but can also trace its data-flow
        /// into a second CALL. Return \b false if this leads us to conclude that the trial is not
        /// a likely parameter.
        /// \param opmatch is the first CALL linked to the trial
        /// \param op is the second CALL
        /// \param vn is the Varnode parameter for the second CALL
        /// \param fl indicates what p-code ops were crossed to reach \e vn
        /// \param trial is the given parameter trial
        /// \return \b true for a legitimate double use
        public bool checkCallDoubleUse(PcodeOp opmatch, PcodeOp op, Varnode vn, TraverseNode.Flags fl,
            ParamTrial trial)
        {
            int j = op.getSlot(vn);
            if (j <= 0) return false;   // Flow traces to indirect call variable, definitely not a param
            FuncCallSpecs fc = getCallSpecs(op);
            FuncCallSpecs matchfc = getCallSpecs(opmatch);
            if (op.code() == opmatch.code()) {
                bool isdirect = (opmatch.code() == OpCode.CPUI_CALL);
                if (   (isdirect && (matchfc.getEntryAddress() == fc.getEntryAddress()))
                    || ((!isdirect) && (op.getIn(0) == opmatch.getIn(0))))
                {
                    // If it is a call to the same function
                    // Varnode addresses are unreliable for this test because copy propagation may have occurred
                    // So we check the actual ParamTrial which holds the original address
                    //	  if (j == 0) return false;
                    ParamTrial curtrial = fc.getActiveInput().getTrialForInputVarnode(j);
                    if (curtrial.getAddress() == trial.getAddress()) {
                        // Check for same memory location
                        if (op.getParent() == opmatch.getParent()) {
                            if (opmatch.getSeqNum().getOrder() < op.getSeqNum().getOrder())
                                return true;    // opmatch has dibs, don't reject
                                                // If use op occurs earlier than match op, we might still need to reject
                        }
                        else
                            // Same function, different basic blocks, assume legit doubleuse
                            return true;
                    }
                }
            }

            if (fc.isInputActive()) {
                ParamTrial curtrial = fc.getActiveInput().getTrialForInputVarnode(j);
                if (curtrial.isChecked()) {
                    if (curtrial.isActive())
                        return false;
                }
                else if (TraverseNode.isAlternatePathValid(vn, fl))
                    return false;
                return true;
            }
            return false;
        }

        /// \brief Test if the given Varnode seems to only be used by a CALL
        ///
        /// Part of testing whether a Varnode makes sense as parameter passing storage is looking for
        /// different explicit uses.
        /// \param invn is the given Varnode
        /// \param opmatch is the putative CALL op using the Varnode for parameter passing
        /// \param trial is the parameter trial object associated with the Varnode
        /// \param mainFlags are flags describing traversals along the \e main path, from \e invn to \e opmatch
        /// \return \b true if the Varnode seems only to be used as parameter to \b opmatch
        public bool onlyOpUse(Varnode invn, PcodeOp opmatch, ParamTrial trial, TraverseNode.Flags mainFlags)
        {
            List<TraverseNode> varlist = new List<TraverseNode>();
            IEnumerator<PcodeOp> iter;
            Varnode vn;
            Varnode? subvn;
            PcodeOp op;
            int i;
            bool res = true;

            varlist.reserve(64);
            invn.setMark();        // Marks prevent infinite loops
            varlist.Add(new TraverseNode(invn, mainFlags));

            for (i = 0; i < varlist.size(); ++i) {
                vn = varlist[i].vn;
                TraverseNode.Flags baseFlags = varlist[i].flags;
                iter = vn.descend.GetEnumerator();
                while (iter.MoveNext()) {
                    op = iter.Current;
                    if (op == opmatch) {
                        if (op.getIn(trial.getSlot()) == vn) continue;
                    }
                    TraverseNode.Flags curFlags = baseFlags;
                    switch (op.code()) {
                        case OpCode.CPUI_BRANCH:       // These ops define a USE of a variable
                        case OpCode.CPUI_CBRANCH:
                        case OpCode.CPUI_BRANCHIND:
                        case OpCode.CPUI_LOAD:
                        case OpCode.CPUI_STORE:
                            res = false;
                            break;
                        case OpCode.CPUI_CALL:
                        case OpCode.CPUI_CALLIND:
                            if (checkCallDoubleUse(opmatch, op, vn, curFlags, trial)) continue;
                            res = false;
                            break;
                        case OpCode.CPUI_INDIRECT:
                            curFlags |= TraverseNode.Flags.indirectalt;
                            break;
                        case OpCode.CPUI_COPY:
                            if (   (op.getOut().getSpace().getType() != spacetype.IPTR_INTERNAL)
                                && !op.isIncidentalCopy() && !vn.isIncidentalCopy())
                            {
                                curFlags |= TraverseNode.Flags.actionalt;
                            }
                            break;
                        case OpCode.CPUI_RETURN:
                            if (opmatch.code() == OpCode.CPUI_RETURN) {
                                // Are we in a different return
                                if (op.getIn(trial.getSlot()) == vn) // But at the same slot
                                    continue;
                            }
                            else if (activeoutput != (ParamActive)null) {
                                // Are we in the middle of analyzing returns
                                if (op.getIn(0) != vn) {
                                    // Unless we hold actual return value
                                    if (!TraverseNode.isAlternatePathValid(vn, curFlags))
                                        // Don't consider this a "use"
                                        continue;
                                }
                            }
                            res = false;
                            break;
                        case OpCode.CPUI_MULTIEQUAL:
                        case OpCode.CPUI_INT_SEXT:
                        case OpCode.CPUI_INT_ZEXT:
                        case OpCode.CPUI_CAST:
                            break;
                        case OpCode.CPUI_PIECE:
                            if (op.getIn(0) == vn) {
                                // Concatenated as most significant piece
                                if ((curFlags & TraverseNode.Flags.lsb_truncated) != 0) {
                                    // Original lsb has been truncated and replaced
                                    continue;   // No longer assume this is a possible use
                                }
                                curFlags |= TraverseNode.Flags.concat_high;
                            }
                            break;
                        case OpCode.CPUI_SUBPIECE:
                            if (op.getIn(1).getOffset() != 0) {
                                // Throwing away least significant byte(s)
                                // If no previous concatenation has occurred
                                if ((curFlags & TraverseNode.Flags.concat_high) == 0)
                                    // Byte(s) of original value have been thrown away
                                    curFlags |= TraverseNode.Flags.lsb_truncated;
                            }
                            break;
                        default:
                            curFlags |= TraverseNode.Flags.actionalt;
                            break;
                    }
                    if (!res) break;
                    subvn = op.getOut();
                    if (subvn != (Varnode)null) {
                        if (subvn.isPersist()) {
                            res = false;
                            break;
                        }
                        if (!subvn.isMark()) {
                            varlist.Add(new TraverseNode(subvn, curFlags));
                            subvn.setMark();
                        }
                    }
                }
                if (!res) break;
            }
            for (i = 0; i < varlist.size(); ++i)
                varlist[i].vn.clearMark();
            return res;
        }

        /// \brief Test if the given trial Varnode is likely only used for parameter passing
        ///
        /// Flow is followed from the Varnode itself and from ancestors the Varnode was copied from
        /// to see if it hits anything other than the given CALL or RETURN operation.
        /// \param maxlevel is the maximum number of times to recurse through ancestor copies
        /// \param invn is the given trial Varnode to test
        /// \param op is the given CALL or RETURN
        /// \param trial is the associated parameter trial object
        /// \param offset is the offset within the current Varnode of the value ultimately copied into the trial
        /// \param mainFlags describes traversals along the path from \e invn to \e op
        /// \return \b true if the Varnode is only used for the CALL/RETURN
        public bool ancestorOpUse(int maxlevel, Varnode invn, PcodeOp op, ParamTrial trial,
            int offset, TraverseNode.Flags mainFlags)
        {
            if (maxlevel == 0) return false;

            if (!invn.isWritten()) {
                if (!invn.isInput()) return false;
                if (!invn.isTypeLock()) return false;
                // If the input is typelocked
                // this is as good as being written
                return onlyOpUse(invn, op, trial, mainFlags); // Test if varnode is only used in op
            }

            PcodeOp def = invn.getDef() ?? throw new BugException();
            switch (def.code()) {
                case OpCode.CPUI_INDIRECT:
                    // An indirectCreation is an indication of an output trial, this should not count as
                    // as an "only use"
                    if (def.isIndirectCreation())
                        return false;
                    return ancestorOpUse(maxlevel - 1, def.getIn(0), op, trial, offset,
                        mainFlags | TraverseNode.Flags.indirect);
                case OpCode.CPUI_MULTIEQUAL:
                    // Check if there is any ancestor whose only
                    // use is in this op
                    if (def.isMark()) return false;    // Trim the loop
                    def.setMark();     // Mark that this MULTIEQUAL is on the path
                                        // Note: onlyOpUse is using Varnode::setMark
                    for (int i = 0; i < def.numInput(); ++i) {
                        if (ancestorOpUse(maxlevel - 1, def.getIn(i), op, trial, offset, mainFlags)) {
                            def.clearMark();
                            return true;
                        }
                    }
                    def.clearMark();
                    return false;
                case OpCode.CPUI_COPY:
                    if ((invn.getSpace().getType() == spacetype.IPTR_INTERNAL)
                        || def.isIncidentalCopy()
                        || def.getIn(0).isIncidentalCopy())
                    {
                        return ancestorOpUse(maxlevel - 1, def.getIn(0), op, trial, offset, mainFlags);
                    }
                    break;
                case OpCode.CPUI_PIECE:
                    // Concatenation tends to be artificial, so recurse through piece corresponding later SUBPIECE
                    if (offset == 0)
                        return ancestorOpUse(maxlevel - 1, def.getIn(1), op, trial, 0, mainFlags); // Follow into least sig piece
                    if (offset == def.getIn(1).getSize())
                        return ancestorOpUse(maxlevel - 1, def.getIn(0), op, trial, 0, mainFlags); // Follow into most sig piece
                    return false;
                case OpCode.CPUI_SUBPIECE: {
                        int newOff = (int)def.getIn(1).getOffset();
                        // This is a rather kludgy way to get around where a DIV (or other similar) instruction
                        // causes a register that looks like the high precision piece of the function return
                        // to be set with the remainder as a side effect
                        if (newOff == 0) {
                            Varnode vn = def.getIn(0);
                            if (vn.isWritten()) {
                                PcodeOp remop = vn.getDef() ?? throw new BugException();
                                if ((remop.code() == OpCode.CPUI_INT_REM) || (remop.code() == OpCode.CPUI_INT_SREM))
                                    trial.setRemFormed();
                            }
                        }
                        if (invn.getSpace().getType() == spacetype.IPTR_INTERNAL || def.isIncidentalCopy() ||
                        def.getIn(0).isIncidentalCopy() ||
                        invn.overlap(def.getIn(0)) == newOff) {
                            return ancestorOpUse(maxlevel - 1, def.getIn(0), op, trial, offset + newOff, mainFlags);
                        }
                        break;
                    }
                case OpCode.CPUI_CALL:
                case OpCode.CPUI_CALLIND:
                    return false;       // A call is never a good indication of a single op use
                default:
                    break;
            }
            // This varnode must be top ancestor at this point
            return onlyOpUse(invn, op, trial, mainFlags); // Test if varnode is only used in op
        }

        /// \brief Update Varnode properties based on (new) Symbol information
        ///
        /// Boolean properties \b addrtied, \b addrforce, and \b nolocalalias
        /// for Varnodes are updated based on new Symbol information they map to.
        /// The caller can elect to update data-type information as well, where Varnodes
        /// and their associated HighVariables have their data-type finalized based symbols.
        /// \param lm is the Symbol scope within which to search for mapped Varnodes
        /// \param updateDatatypes is \b true if the caller wants to update data-types
        /// \param unmappedAliasCheck is \b true if an alias check should be performed on unmapped Varnodes
        /// \return \b true if any Varnode was updated
        public bool syncVarnodesWithSymbols(ScopeLocal lm, bool updateDatatypes, bool unmappedAliasCheck)
        {
            bool updateoccurred = false;
            Datatype? ct;
            SymbolEntry? entry;
            Varnode.varnode_flags fl;

            IEnumerator<Varnode> iter = vbank.beginLoc(lm.getSpaceId());
            enditer = vbank.endLoc(lm.getSpaceId());
            while (iter != enditer) {
                Varnode vnexemplar = iter.Current;
                entry = lm.findOverlap(vnexemplar.getAddr(), vnexemplar.getSize());
                ct = (Datatype)null;
                if (entry != (SymbolEntry)null) {
                    fl = entry.getAllFlags();
                    if (entry.getSize() >= vnexemplar.getSize()) {
                        if (updateDatatypes) {
                            ct = entry.getSizedType(vnexemplar.getAddr(), vnexemplar.getSize());
                            if (ct != (Datatype)null && ct.getMetatype() == type_metatype.TYPE_UNKNOWN)
                                ct = (Datatype)null;
                        }
                    }
                    else {
                        // Overlapping but not containing
                        // This is usual indicative of a small locked symbol
                        // getting put in a bigger register
                        // Don't try to figure out type
                        // Don't keep typelock and namelock
                        fl &= ~(Varnode.varnode_flags.typelock | Varnode.varnode_flagsnamelock);
                        // we do particularly want to keep the nolocalalias
                    }
                }
                else {
                    // Could not find any symbol
                    if (lm.inScope(vnexemplar.getAddr(), vnexemplar.getSize(), vnexemplar.getUsePoint(this))) {
                        // This is technically an error, there should be some
                        // kind of symbol, if we are in scope
                        fl = Varnode.varnode_flags.mapped | Varnode.varnode_flags.addrtied;
                    }
                    else if (unmappedAliasCheck) {
                        // If the varnode is not in scope, check if we should treat as unaliased
                        fl = lm.isUnmappedUnaliased(vnexemplar) ? Varnode.varnode_flags.nolocalalias : 0;
                    }
                    else
                        fl = 0;
                }
                if (syncVarnodesWithSymbol(iter, fl, ct))
                    updateoccurred = true;
            }
            return updateoccurred;
        }

        /// \brief Copy properties from an existing Varnode to a new Varnode
        ///
        /// The new Varnode is assumed to overlap the storage of the existing Varnode.
        /// Properties like boolean flags and \e consume bits are copied as appropriate.
        /// \param vn is the existing Varnode
        /// \param newVn is the new Varnode that has its properties set
        /// \param lsbOffset is the significance offset of the new Varnode within the exising
        public void transferVarnodeProperties(Varnode vn, Varnode newVn, int lsbOffset)
        {
            ulong newConsume = (vn.getConsume() >> 8 * lsbOffset) & Globals.calc_mask((uint)newVn.getSize());

            Varnode.varnode_flags vnFlags = vn.getFlags()
                & (Varnode.varnode_flags.directwrite | Varnode.varnode_flags.addrforce);

            newVn.setFlags(vnFlags);   // Preserve addrforce setting
            newVn.setConsume(newConsume);
        }

        /// Replace the given Varnode with its (constant) value in the load image
        /// Treat the given Varnode as read-only, look up its value in LoadImage
        /// and replace read references with the value as a constant Varnode.
        /// \param vn is the given Varnode
        /// \return \b true if any change was made
        public bool fillinReadOnly(Varnode vn)
        {
            if (vn.isWritten()) {
                // Can't replace output with constant
                PcodeOp? defop = vn.getDef() ?? throw new BugException();
                if (defop.isMarker())
                    defop.setAdditionalFlag(PcodeOp.AdditionalFlags.warning); // Not a true write, ignore it
                else if (!defop.isWarning()) {
                    // No warning generated before
                    defop.setAdditionalFlag(PcodeOp.AdditionalFlags.warning);
                    if ((!vn.isAddrForce()) || (!vn.hasNoDescend())) {
                        StringWriter s = new StringWriter();
                        s.Write($"Read-only address ({vn.getSpace().getName()},");
                        vn.getAddr().printRaw(s);
                        s.Write(") is written");
                        warning(s.ToString(), defop.getAddr());
                    }
                }
                return false;       // No change was made
            }

            if (vn.getSize() > sizeof(ulong))
                return false;       // Constant will exceed precision

            ulong res;
            byte[] bytes = new byte[32];
            try {
                glb.loader.loadFill(bytes, vn.getSize(), vn.getAddr());
            }
            catch (DataUnavailError err) { // Could not get value from LoadImage
                vn.clearFlags(Varnode.varnode_flags.@readonly); // Treat as writeable
                return true;
            }

            if (vn.getSpace().isBigEndian()) {
                // Big endian
                res = 0;
                for (int i = 0; i < vn.getSize(); ++i) {
                    res <<= 8;
                    res |= bytes[i];
                }
            }
            else {
                res = 0;
                for (int i = vn.getSize() - 1; i >= 0; --i) {
                    res <<= 8;
                    res |= bytes[i];
                }
            }
            // Replace all references to vn
            bool changemade = false;
            PcodeOp op;
            Datatype? locktype = vn.isTypeLock() ? vn.getType() : (Datatype)null;

            IEnumerator<PcodeOp> iter = vn.beginDescend();
            while (iter.MoveNext()) {
                op = iter.Current;
                int i = op.getSlot(vn);
                if (op.isMarker()) {
                    // Must be careful putting constants in here
                    if ((op.code() != OpCode.CPUI_INDIRECT) || (i != 0)) continue;
                    Varnode outvn = op.getOut();
                    if (outvn.getAddr() == vn.getAddr()) continue; // Ignore indirect to itself
                                                                     // Change the indirect to a COPY
                    opRemoveInput(op, 1);
                    opSetOpcode(op, OpCode.CPUI_COPY);
                }
                Varnode cvn = newConstant(vn.getSize(), res);
                if (locktype != (Datatype)null)
                    cvn.updateType(locktype, true, true); // Try to pass on the locked datatype
                opSetInput(op, cvn, i);
                changemade = true;
            }
            return changemade;
        }

        /// Replace accesses of the given Varnode with \e volatile operations
        /// The Varnode is assumed not fully linked.  The read or write action is
        /// modeled by inserting a special \e user op that represents the action. The given Varnode is
        /// replaced by a temporary Varnode within the data-flow, and the original address becomes
        /// a parameter to the user op.
        /// \param vn is the given Varnode to model as volatile
        /// \return \b true if a change was made
        public bool replaceVolatile(Varnode vn)
        {
            PcodeOp newop;
            if (vn.isWritten()) {
                // A written value
                VolatileWriteOp vw_op = glb.userops.getVolatileWrite();
                if (!vn.hasNoDescend()) throw new LowlevelError("Volatile memory was propagated");
                PcodeOp defop = vn.getDef() ?? throw new BugException();
                newop = newOp(3, defop.getAddr());
                opSetOpcode(newop, OpCode.CPUI_CALLOTHER);
                // Create a userop of type specified by vw_op
                opSetInput(newop, newConstant(4, (ulong)vw_op.getIndex()), 0);
                // The first parameter is the offset of volatile memory location
                Varnode annoteVn = newCodeRef(vn.getAddr());
                annoteVn.setFlags(Varnode.varnode_flags.volatil);
                opSetInput(newop, annoteVn, 1);
                // Replace the volatile variable with a temp
                Varnode tmp = newUnique(vn.getSize());
                opSetOutput(defop, tmp);
                // The temp is the second parameter to the userop
                opSetInput(newop, tmp, 2);
                opInsertAfter(newop, defop); // Insert after defining op
            }
            else
            {           // A read value
                VolatileReadOp vr_op = glb.userops.getVolatileRead();
                if (vn.hasNoDescend()) return false; // Dead
                PcodeOp readop = vn.loneDescend();
                if (readop == (PcodeOp)null)
                    throw new LowlevelError("Volatile memory value used more than once");
                newop = newOp(2, readop.getAddr());
                opSetOpcode(newop, OpCode.CPUI_CALLOTHER);
                // Create a temp to replace the volatile variable
                Varnode tmp = newUniqueOut(vn.getSize(), newop);
                // Create a userop of type specified by vr_op
                opSetInput(newop, newConstant(4, (ulong)vr_op.getIndex()), 0);
                // The first parameter is the offset of the volatile memory loc
                Varnode annoteVn = newCodeRef(vn.getAddr());
                annoteVn.setFlags(Varnode.varnode_flags.volatil);
                opSetInput(newop, annoteVn, 1);
                opSetInput(readop, tmp, readop.getSlot(vn));
                opInsertBefore(newop, readop); // Insert before read
                if (vr_op.getDisplay() != 0)   // Unless the display is functional,
                    newop.setHoldOutput();     // read value may not be used. Keep it around anyway.
            }
            if (vn.isTypeLock())       // If the original varnode had a type locked on it
                newop.setAdditionalFlag(PcodeOp.AdditionalFlags.special_prop); // Mark this op as doing special propagation
            return true;
        }

        /// Mark \e illegal \e input Varnodes used only in INDIRECTs
        /// The illegal inputs are additionally marked as \b indirectonly and
        /// isIndirectOnly() returns \b true.
        public void markIndirectOnly()
        {
            VarnodeDefSet::const_iterator iter, enditer;

            iter = beginDef(Varnode.varnode_flags.input);
            enditer = endDef(Varnode.varnode_flags.input);
            for (; iter != enditer; ++iter) {
                // Loop over all inputs
                Varnode vn = iter.Current;
                if (!vn.isIllegalInput()) continue; // Only check illegal inputs
                if (checkIndirectUse(vn))
                    vn.setFlags(Varnode.varnode_flags.indirectonly);
            }
        }

        /// \brief Replace all read references to the first Varnode with a second Varnode
        /// \param vn is the first Varnode (being replaced)
        /// \param newvn is the second Varnode (the replacement)
        public void totalReplace(Varnode vn, Varnode newvn)
        {
            PcodeOp op;
            int i;

            IEnumerator<PcodeOp> iter = vn.beginDescend();
            while (iter.MoveNext()) {
                // Increment before removing descendant
                op = iter.Current;
                i = op.getSlot(vn);
                opSetInput(op, newvn, i);
            }
        }

        /// \brief Replace every read reference of the given Varnode with a constant value
        ///
        /// A new constant Varnode is created for each read site. If there are any marker ops
        /// (MULTIEQUAL) a single COPY op is inserted and the marker input is set to be the
        /// output of the COPY.
        /// \param vn is the given Varnode
        /// \param val is the constant value to replace it with
        public void totalReplaceConstant(Varnode vn, ulong val)
        {
            PcodeOp op;
            PcodeOp? copyop = (PcodeOp)null;
            Varnode newrep;
            int i;

            IEnumerator<PcodeOp> iter = vn.beginDescend();
            while (iter.MoveNext()) {
                // WARNING : see behavior
                throw new NotImplementedException();
                op = iter++;       // Increment before removing descendant
                i = op.getSlot(vn);
                if (op.isMarker()) {
                    // Do not put constant directly in marker
                    if (copyop == (PcodeOp)null) {
                        if (vn.isWritten()) {
                            copyop = newOp(1, vn.getDef().getAddr());
                            opSetOpcode(copyop, OpCode.CPUI_COPY);
                            newrep = newUniqueOut(vn.getSize(), copyop);
                            opSetInput(copyop, newConstant(vn.getSize(), val), 0);
                            opInsertAfter(copyop, vn.getDef());
                        }
                        else {
                            BlockBasic bb = (BlockBasic)getBasicBlocks().getBlock(0);
                            copyop = newOp(1, bb.getStart());
                            opSetOpcode(copyop, OpCode.CPUI_COPY);
                            newrep = newUniqueOut(vn.getSize(), copyop);
                            opSetInput(copyop, newConstant(vn.getSize(), val), 0);
                            opInsertBegin(copyop, bb);
                        }
                    }
                    else
                        newrep = copyop.getOut();
                }
                else
                    newrep = newConstant(vn.getSize(), val);
                opSetInput(op, newrep, i);
            }
        }

        /// Get the local function scope
        public ScopeLocal getScopeLocal() => localmap;

        /// Get the function's prototype object
        public FuncProto getFuncProto() => funcp;

        /// Initialize \e return prototype recovery analysis
        public void initActiveOutput()
        {
            activeoutput = new ParamActive(false);
            int maxdelay = funcp.getMaxOutputDelay();
            if (maxdelay > 0)
                maxdelay = 3;
            activeoutput.setMaxPass(maxdelay);
        }

        /// \brief Clear any analysis of the function's \e return prototype
        public void clearActiveOutput()
        {
            //if (activeoutput != (ParamActive)null) delete activeoutput;
            activeoutput = (ParamActive)null;
        }

        /// Get the \e return prototype recovery object
        public ParamActive getActiveOutput() => activeoutput;

        /// Turn on HighVariable objects for all Varnodes
        public void setHighLevel()
        {
            if ((flags & Flags.highlevel_on) != 0) return;
            flags |= Flags.highlevel_on;
            high_level_index = vbank.getCreateIndex();
            VarnodeLocSet::const_iterator iter;

            for (iter = vbank.beginLoc(); iter != vbank.endLoc(); ++iter)
                assignHigh(*iter);
        }

        /// Delete any dead Varnodes
        /// Free any Varnodes not attached to anything. This is only performed at fixed times so that
        /// editing operations can detach (and then reattach) Varnodes without losing them.
        public void clearDeadVarnodes()
        {
            VarnodeLocSet::const_iterator iter;
            Varnode vn;

            iter = vbank.beginLoc();
            while (iter != vbank.endLoc()) {
                vn = *iter++;
                if (vn.hasNoDescend()) {
                    if (vn.isInput() && !vn.isLockedInput()) {
                        vbank.makeFree(vn);
                        vn.clearCover();
                    }
                    if (vn.isFree())
                        vbank.destroy(vn);
                }
            }
        }

        /// Calculate \e non-zero masks for all Varnodes
        /// All Varnodes are initialized assuming that all its bits are possibly non-zero. This method
        /// looks for situations where a p-code produces a value that is known to have some bits that are
        /// guaranteed to be zero.  It updates the state of the output Varnode then tries to push the
        /// information forward through the data-flow until additional changes are apparent.
        public void calcNZMask()
        {
            List<PcodeOpNode> opstack = new List<PcodeOpNode>();
            IEnumerator<PcodeOp> oiter = beginOpAlive();

            while (oiter.MoveNext()) {
                PcodeOp op = oiter.Current;
                if (op.isMark()) continue;
                opstack.Add(new PcodeOpNode(op, 0));
                op.setMark();

                do {
                    // Get next edge
                    PcodeOpNode node = opstack.GetLastItem();
                    if (node.slot >= node.op.numInput()) {
                        // If no edge left
                        Varnode outvn = node.op.getOut();
                        if (outvn != (Varnode)null) {
                            outvn.nzm = node.op.getNZMaskLocal(true);
                        }
                        opstack.RemoveLastItem(); // Pop a level
                        continue;
                    }
                    int oldslot = node.slot;
                    node.slot += 1; // Advance to next input
                                    // Determine if we want to traverse this edge
                    if (node.op.code() == OpCode.CPUI_MULTIEQUAL)
                    {
                        if (node.op.getParent().isLoopIn(oldslot)) // Clip looping edges
                            continue;
                    }
                    // Traverse edge indicated by slot
                    Varnode vn = node.op.getIn(oldslot);
                    if (!vn.isWritten()) {
                        if (vn.isConstant())
                            vn.nzm = vn.getOffset();
                        else {
                            vn.nzm = Globals.calc_mask((uint)vn.getSize());
                            if (vn.isSpacebase())
                                vn.nzm &= ~((ulong)0xff); // Treat spacebase input as aligned
                        }
                    }
                    else if (!vn.getDef().isMark()) {
                        // If haven't traversed before
                        opstack.Add(new PcodeOpNode(vn.getDef(), 0));
                        vn.getDef().setMark();
                    }
                } while (0 != opstack.Count);
            }

            List<PcodeOp> worklist = new List<PcodeOp>();
            // Clear marks and push ops with looping edges onto worklist
            oiter = beginOpAlive();
            while (oiter.MoveNext()) {
                PcodeOp op = oiter.Current;
                op.clearMark();
                if (op.code() == OpCode.CPUI_MULTIEQUAL)
                    worklist.Add(op);
            }

            // Continue to propagate changes along all edges
            while (0 != worklist.Count) {
                PcodeOp op = worklist.GetLastItem();
                worklist.RemoveLastItem();
                Varnode vn = op.getOut();
                if (vn == (Varnode)null) continue;
                ulong nzmask = op.getNZMaskLocal(false);
                if (nzmask != vn.nzm) {
                    vn.nzm = nzmask;
                    oiter = vn.beginDescend();
                    worklist.AddRange(oiter);
                }
            }
        }

        /// Delete any dead PcodeOps
        public void clearDeadOps()
        {
            obank.destroyDead();
        }

        /// \brief Remap a Symbol to a given Varnode using a static mapping
        ///
        /// Any previous links between the Symbol, the Varnode, and the associate HighVariable are
        /// removed.  Then a new link is created.
        /// \param vn is the given Varnode
        /// \param sym is the Symbol the Varnode maps to
        /// \param usepoint is the desired usepoint for the mapping
        public void remapVarnode(Varnode vn, Symbol sym, Address usepoint)
        {
            vn.clearSymbolLinks();
            SymbolEntry entry = localmap.remapSymbol(sym, vn.getAddr(), usepoint);
            vn.setSymbolEntry(entry);
        }

        /// \brief Remap a Symbol to a given Varnode using a new dynamic mapping
        ///
        /// Any previous links between the Symbol, the Varnode, and the associate HighVariable are
        /// removed.  Then a new dynamic link is created.
        /// \param vn is the given Varnode
        /// \param sym is the Symbol the Varnode maps to
        /// \param usepoint is the code Address where the Varnode is defined
        /// \param hash is the hash for the new dynamic mapping
        public void remapDynamicVarnode(Varnode vn, Symbol sym, Address usepoint, ulong hash)
        {
            vn.clearSymbolLinks();
            SymbolEntry entry = localmap.remapSymbolDynamic(sym, hash, usepoint);
            vn.setSymbolEntry(entry);
        }

        /// Find or create Symbol and a partial mapping
        /// PIECE operations put the given Varnode into a larger structure.  Find the resulting
        /// whole Varnode, make sure it has a symbol assigned, and then assign the same symbol
        /// to the given Varnode piece.  If the given Varnode has been merged with something
        /// else or the whole Varnode can't be found, do nothing.
        public void linkProtoPartial(Varnode vn)
        {
            HighVariable high = vn.getHigh();
            if (high.getSymbol() != (Symbol)null) return;
            Varnode rootVn = PieceNode.findRoot(vn);
            if (rootVn == vn) return;

            HighVariable rootHigh = rootVn.getHigh();
            Varnode nameRep = rootHigh.getNameRepresentative();
            Symbol? sym = linkSymbol(nameRep);
            if (sym == (Symbol)null) return;
            rootHigh.establishGroupSymbolOffset();
            SymbolEntry entry = sym.getFirstWholeMap();
            vn.setSymbolEntry(entry);
        }

        /// Find or create Symbol associated with given Varnode
        /// The Symbol is really attached to the Varnode's HighVariable (which must exist).
        /// The only reason a Symbol doesn't get set is if, the HighVariable
        /// is global and there is no pre-existing Symbol.  (see mapGlobals())
        /// \param vn is the given Varnode
        /// \return the associated Symbol or NULL
        public Symbol? linkSymbol(Varnode vn)
        {
            if (vn.isProtoPartial())
                linkProtoPartial(vn);
            HighVariable high = vn.getHigh();
            SymbolEntry? entry;
            Varnode.varnode_flags fl = 0;
            Symbol? sym = high.getSymbol();
            if (sym != (Symbol)null) return sym; // Symbol already assigned

            Address usepoint = vn.getUsePoint(this);
            // Find any entry overlapping base address
            entry = localmap.queryProperties(vn.getAddr(), 1, usepoint, out fl);
            if (entry != (SymbolEntry)null) {
                sym = handleSymbolConflict(entry, vn);
            }
            else {
                // Must create a symbol entry
                if (!vn.isPersist()) {
                    // Only create local symbol
                    if (vn.isAddrTied())
                        usepoint = new Address();
                    entry = localmap.addSymbol("", high.getType(), vn.getAddr(), usepoint);
                    sym = entry.getSymbol();
                    vn.setSymbolEntry(entry);
                }
            }
            return sym;
        }

        /// Discover and attach Symbol to a constant reference
        /// A reference to a symbol (i.e. &varname) is typically stored as a PTRSUB operation, where the
        /// first input Varnode is a \e spacebase Varnode indicating whether the symbol is on the \e stack or at
        /// a \e global RAM location.  The second input Varnode is a constant encoding the address of the symbol.
        /// This method takes this constant Varnode, recovers the symbol it is referring to, and stores
        /// on the HighVariable object attached to the Varnode.
        /// \param vn is the constant Varnode (second input) to a PTRSUB operation
        /// \return the symbol being referred to or null
        public Symbol? linkSymbolReference(Varnode vn)
        {
            PcodeOp op = vn.loneDescend();
            Varnode in0 = op.getIn(0);
            TypePointer ptype = (TypePointer)in0.getHigh().getType() ?? throw new BugException();
            if (ptype.getMetatype() != type_metatype.TYPE_PTR) return (Symbol)null;
            TypeSpacebase sb = (TypeSpacebase)ptype.getPtrTo();
            if (sb.getMetatype() != type_metatype.TYPE_SPACEBASE) return (Symbol)null;
            Scope scope = sb.getMap();
            Address addr = sb.getAddress(vn.getOffset(), in0.getSize(), op.getAddr());
            if (addr.isInvalid())
                throw new LowlevelError("Unable to generate proper address from spacebase");
            SymbolEntry entry = scope.queryContainer(addr, 1, new Address());
            if (entry == (SymbolEntry)null) return (Symbol)null;
            int off = (int)(addr.getOffset() - entry.getAddr().getOffset()) + entry.getOffset();
            vn.setSymbolReference(entry, off);
            return entry.getSymbol();
        }

        /// Find a Varnode matching the given Symbol mapping
        /// Return the (first) Varnode that matches the given SymbolEntry
        /// \param entry is the given SymbolEntry
        /// \return a matching Varnode or null
        public Varnode? findLinkedVarnode(SymbolEntry entry)
        {
            if (entry.isDynamic()) {
                DynamicHash dhash = new DynamicHash();
                Varnode? vn = dhash.findVarnode(this, entry.getFirstUseAddress(), entry.getHash());
                if (vn == (Varnode)null || vn.isAnnotation())
                    return (Varnode)null;
                return vn;
            }

            VarnodeLocSet::const_iterator iter, enditer;
            Address usestart = entry.getFirstUseAddress();
            enditer = vbank.endLoc(entry.getSize(), entry.getAddr());

            if (usestart.isInvalid()) {
                iter = vbank.beginLoc(entry.getSize(), entry.getAddr());
                if (iter == enditer)
                    return (Varnode)null;
                Varnode vn = iter.Current;
                if (!vn.isAddrTied())
                    return (Varnode)null; // Varnode(s) must be address tied in order to match this symbol
                return vn;
            }
            iter = vbank.beginLoc(entry.getSize(), entry.getAddr(), usestart, uint.MaxValue);
            // TODO: Use a better end iterator
            for (; iter != enditer; ++iter) {
                Varnode vn = iter.Current;
                Address usepoint = vn.getUsePoint(this);
                if (entry.inUse(usepoint))
                    return vn;
            }
            return (Varnode)null;
        }

        /// Find Varnodes that map to the given SymbolEntry
        /// Look for Varnodes that are (should be) mapped to the given SymbolEntry and
        /// add them to the end of the result list.
        /// \param entry is the given SymbolEntry to match
        /// \param res is the container holding the result list of matching Varnodes
        public void findLinkedVarnodes(SymbolEntry entry, List<Varnode> res)
        {
            if (entry.isDynamic()) {
                DynamicHash dhash = new DynamicHash();
                Varnode? vn = dhash.findVarnode(this, entry.getFirstUseAddress(), entry.getHash());
                if (vn != (Varnode)null)
                    res.Add(vn);
            }
            else {
                VarnodeLocSet::const_iterator iter = beginLoc(entry.getSize(), entry.getAddr());
                VarnodeLocSet::const_iterator enditer = endLoc(entry.getSize(), entry.getAddr());
                for (; iter != enditer; ++iter) {
                    Varnode vn = iter.Current;
                    Address addr = vn.getUsePoint(this);
                    if (entry.inUse(addr)) {
                        res.Add(vn);
                    }
                }
            }
        }

        /// Build a \e dynamic Symbol associated with the given Varnode
        /// If a Symbol is already attached, no change is made. Otherwise a special \e dynamic Symbol is
        /// created that is associated with the Varnode via a hash of its local data-flow (rather
        /// than its storage address).
        /// \param vn is the given Varnode
        public void buildDynamicSymbol(Varnode vn)
        {
            if (vn.isTypeLock() || vn.isNameLock())
                throw new RecovError("Trying to build dynamic symbol on locked varnode");
            if (!isHighOn())
                throw new RecovError("Cannot create dynamic symbols until decompile has completed");
            HighVariable high = vn.getHigh();
            if (high.getSymbol() != (Symbol)null)
                return;         // Symbol already exists
            DynamicHash dhash = new DynamicHash();

            dhash.uniqueHash(vn, this); // Calculate a unique dynamic hash for this varnode
            if (dhash.getHash() == 0)
                throw new RecovError("Unable to find unique hash for varnode");

            Symbol sym;
            if (vn.isConstant())
                sym = localmap.addEquateSymbol("", Symbol.DisplayFlags.force_hex, vn.getOffset(), dhash.getAddress(), dhash.getHash());
            else
                sym = localmap.addDynamicSymbol("", high.getType(), dhash.getAddress(), dhash.getHash());
            vn.setSymbolEntry(sym.getFirstWholeMap());
        }

        /// \brief Map properties of a dynamic symbol to a Varnode
        ///
        /// Given a dynamic mapping, try to find the mapped Varnode, then adjust (type and flags)
        /// to reflect this mapping.
        /// \param entry is the (dynamic) Symbol entry
        /// \param dhash is the dynamic mapping information
        /// \return \b true if a Varnode was adjusted
        public bool attemptDynamicMapping(SymbolEntry entry, DynamicHash dhash)
        {
            Symbol sym = entry.getSymbol();
            if (sym.getScope() != localmap)
                throw new LowlevelError("Cannot currently have a dynamic symbol outside the local scope");
            dhash.clear();
            Symbol.SymbolCategory category = sym.getCategory();
            if (category == Symbol.SymbolCategory.union_facet) {
                return applyUnionFacet(entry, dhash);
            }
            Varnode? vn = dhash.findVarnode(this, entry.getFirstUseAddress(), entry.getHash());
            if (vn == (Varnode)null) return false;
            if (vn.getSymbolEntry() != (SymbolEntry)null) return false;  // Varnode is already labeled
            if (category == Symbol.SymbolCategory.equate) {
                // Is this an equate symbol
                vn.setSymbolEntry(entry);
                return true;
            }
            else if (entry.getSize() == vn.getSize()) {
                if (vn.setSymbolProperties(entry))
                    return true;
            }
            return false;
        }

        /// \brief Map the name of a dynamic symbol to a Varnode
        ///
        /// Given a dynamic mapping, try to find the mapped Varnode, then attach the Symbol to the Varnode.
        /// The name of the Symbol is used, but the data-type and possibly other properties are not
        /// put on the Varnode.
        /// \param entry is the (dynamic) Symbol entry
        /// \param dhash is the dynamic mapping information
        /// \return \b true if a Varnode was adjusted
        public bool attemptDynamicMappingLate(SymbolEntry entry, DynamicHash dhash)
        {
            dhash.clear();
            Symbol sym = entry.getSymbol();
            if (sym.getCategory() == Symbol.SymbolCategory.union_facet) {
                return applyUnionFacet(entry, dhash);
            }
            Varnode? vn = dhash.findVarnode(this, entry.getFirstUseAddress(), entry.getHash());
            if (vn == (Varnode)null)
                return false;
            if (vn.getSymbolEntry() != (SymbolEntry)null) return false; // Symbol already applied
            if (sym.getCategory() == Symbol.SymbolCategory.equate)
            {   // Equate symbol does not depend on size
                vn.setSymbolEntry(entry);
                return true;
            }
            if (vn.getSize() != entry.getSize())
            {
                StringWriter s = new StringWriter();
                s.Write("Unable to use symbol ");
                if (!sym.isNameUndefined())
                    s.Write($"{sym.getName()} ");
                s.Write(": Size does not match variable it labels");
                warningHeader(s.ToString());
                return false;
            }

            if (vn.isImplied()) {
                // This should be finding an explicit, but a cast may have been inserted
                Varnode? newvn = (Varnode)null;
                // Look at the "other side" of the cast
                if (vn.isWritten() && (vn.getDef().code() == OpCode.CPUI_CAST))
                    newvn = vn.getDef().getIn(0);
                else {
                    PcodeOp? castop = vn.loneDescend();
                    if ((castop != (PcodeOp)null) && (castop.code() == OpCode.CPUI_CAST))
                        newvn = castop.getOut();
                }
                // See if the varnode on the other side is explicit
                if ((newvn != (Varnode)null) && (newvn.isExplicit()))
                    vn = newvn;     // in which case we use it
            }

            vn.setSymbolEntry(entry);
            if (!sym.isTypeLocked()) {
                // If the dynamic symbol did not lock its type
                localmap.retypeSymbol(sym, vn.getType()); // use the type propagated into the varnode
            }
            else if (sym.getType() != vn.getType()) {
                warningHeader($"Unable to use type for symbol {sym.getName()}");
                localmap.retypeSymbol(sym, vn.getType()); // use the type propagated into the varnode
            }
            return true;
        }

        /// Get the Merge object for \b this function
        public Merge getMerge() => covermerge;

        // op routines

        /// Allocate a new PcodeOp with Address
        /// \param inputs is the number of operands the new op will have
        /// \param pc is the Address associated with the new op
        /// \return the new PcodeOp
        public PcodeOp newOp(int inputs, Address pc)
        {
            return obank.create(inputs, pc);
        }

        /// Allocate a new PcodeOp with sequence number
        /// This method is typically used for cloning.
        /// \param inputs is the number of operands the new op will have
        /// \param sq is the sequence number (Address and sub-index) of the new op
        /// \return the new PcodeOp
        public PcodeOp newOp(int inputs, SeqNum sq)
        {
            return obank.create(inputs, sq);
        }

        /// \brief Create new PcodeOp with 2 or 3 given operands
        ///
        /// The new op will have a \e unique space output Varnode and will be inserted before
        /// the given \e follow op.
        /// \param follow is the \e follow up to insert the new PcodeOp before
        /// \param opc is the op-code of the new PcodeOp
        /// \param in1 is the first operand
        /// \param in2 is the second operand
        /// \param in3 is the optional third param
        /// \return the new PcodeOp
        public PcodeOp newOpBefore(PcodeOp follow, OpCode opc, Varnode in1, Varnode in2,
            Varnode? in3 = null)
        {
            int sz;

            sz = (in3 == (Varnode)null) ? 2 : 3;
            PcodeOp newop = newOp(sz, follow.getAddr());
            opSetOpcode(newop, opc);
            newUniqueOut(in1.getSize(), newop);
            opSetInput(newop, in1, 0);
            opSetInput(newop, in2, 1);
            if (sz == 3)
                opSetInput(newop, in3 ?? throw new BugException(), 2);
            opInsertBefore(newop, follow);
            return newop;
        }

        /// Clone a PcodeOp into \b this function
        /// Make a clone of the given PcodeOp, copying control-flow properties as well.  The data-type
        /// is \e not cloned.
        /// \param op is the PcodeOp to clone
        /// \param seq is the (possibly custom) sequence number to associate with the clone
        /// \return the cloned PcodeOp
        public PcodeOp cloneOp(PcodeOp op, SeqNum seq)
        {
            PcodeOp newop = newOp(op.numInput(), seq);
            opSetOpcode(newop, op.code());
            PcodeOp.Flags fl = op.flags & (PcodeOp.Flags.startmark | PcodeOp.Flags.startbasic);
            newop.setFlag(fl);
            if (op.getOut() != (Varnode)null)
                opSetOutput(newop, cloneVarnode(op.getOut()));
            for (int i = 0; i < op.numInput(); ++i)
                opSetInput(newop, cloneVarnode(op.getIn(i)), i);
            return newop;
        }

        /// Find a representative OpCode.CPUI_RETURN op for \b this function
        /// Return the first OpCode.CPUI_RETURN operation that is not dead or an artificial halt
        /// \return a representative OpCode.CPUI_RETURN op or NULL if there are none
        public PcodeOp? getFirstReturnOp()
        {
            IEnumerator<PcodeOp> iter = beginOp(OpCode.CPUI_RETURN);
            while (iter.MoveNext()) {
                PcodeOp retop = iter.Current;
                if (retop.isDead()) continue;
                if (retop.getHaltType() != 0) continue;
                return retop;
            }
            return (PcodeOp)null;
        }

        /// \brief Create a new OpCode.CPUI_INDIRECT around a PcodeOp with an indirect effect
        ///
        /// Typically this is used to annotate data-flow, for the given storage range, passing
        /// through a CALL or STORE. An output Varnode is automatically created.
        /// \param indeffect is the PcodeOp with the indirect effect
        /// \param addr is the starting address of the storage range to protect
        /// \param sz is the number of bytes in the storage range
        /// \param extraFlags are extra boolean properties to put on the INDIRECT
        /// \return the new OpCode.CPUI_INDIRECT op
        public PcodeOp newIndirectOp(PcodeOp indeffect, Address addr, int sz, PcodeOp.Flags extraFlags)
        {
            Varnode newin = newVarnode(sz, addr);
            PcodeOp newop = newOp(2, indeffect.getAddr());

            newop.flags |= extraFlags;
            newVarnodeOut(sz, addr, newop);
            opSetOpcode(newop, OpCode.CPUI_INDIRECT);
            opSetInput(newop, newin, 0);
            opSetInput(newop, newVarnodeIop(indeffect), 1);
            opInsertBefore(newop, indeffect);
            return newop;
        }

        /// \brief Build a OpCode.CPUI_INDIRECT op that \e indirectly \e creates a Varnode
        ///
        /// An \e indirectly \e created Varnode effectively has no data-flow before the INDIRECT op
        /// that defines it, and the value contained by the Varnode is not explicitly calculable.
        /// The new Varnode is allocated with a given storage range.
        /// \param indeffect is the p-code causing the indirect effect
        /// \param addr is the starting address of the given storage range
        /// \param sz is the number of bytes in the storage range
        /// \param possibleout is \b true if the output should be treated as a \e directwrite.
        /// \return the new OpCode.CPUI_INDIRECT op
        public PcodeOp newIndirectCreation(PcodeOp indeffect, Address addr, int sz, bool possibleout)
        {
            Varnode newin = newConstant(sz, 0);
            PcodeOp newop = newOp(2, indeffect.getAddr());
            newop.flags |= PcodeOp.Flags.indirect_creation;
            Varnode newout = newVarnodeOut(sz, addr, newop);
            if (!possibleout)
                newin.flags |= Varnode.varnode_flags.indirect_creation;
            newout.flags |= Varnode.varnode_flags.indirect_creation;
            opSetOpcode(newop, OpCode.CPUI_INDIRECT);
            opSetInput(newop, newin, 0);
            opSetInput(newop, newVarnodeIop(indeffect), 1);
            opInsertBefore(newop, indeffect);
            return newop;
        }

        /// Convert OpCode.CPUI_INDIRECT into an \e indirect \e creation
        /// Data-flow through the given OpCode.CPUI_INDIRECT op is marked so that the output Varnode
        /// is considered \e indirectly \e created.
        /// An \e indirectly \e created Varnode effectively has no data-flow before the INDIRECT op
        /// that defines it, and the value contained by the Varnode is not explicitly calculable.
        /// \param indop is the given OpCode.CPUI_INDIRECT op
        /// \param possibleOutput is \b true if INDIRECT should be marked as a possible call output
        public void markIndirectCreation(PcodeOp indop, bool possibleOutput)
        {
            Varnode outvn = indop.getOut();
            Varnode in0 = indop.getIn(0);

            indop.flags |= PcodeOp.Flags.indirect_creation;
            if (!in0.isConstant())
                throw new LowlevelError("Indirect creation not properly formed");
            if (!possibleOutput)
                in0.flags |= Varnode.varnode_flags.indirect_creation;
            outvn.flags |= Varnode.varnode_flags.indirect_creation;
        }

        /// Find PcodeOp with given sequence number
        public PcodeOp findOp(SeqNum sq) => obank.findOp(sq);

        /// Insert given PcodeOp before a specific op
        /// The given PcodeOp is inserted \e immediately before the \e follow op except:
        ///  - MULTIEQUALS in a basic block all occur first
        ///  - INDIRECTs occur immediately before their op
        ///  - a branch op must be the very last op in a basic block
        ///
        /// \param op is the given PcodeOp to insert
        /// \param follow is the op to insert before
        public void opInsertBefore(PcodeOp op, PcodeOp follow)
        {
            IEnumerator<PcodeOp> iter = follow.getBasicIter();
            BlockBasic parent = follow.getParent();

            if (op.code() != OpCode.CPUI_INDIRECT) {
                // There should not be an INDIRECT immediately preceding op
                PcodeOp previousop;
                while (iter != parent.beginOp()) {
                    --iter;
                    previousop = iter.Current;
                    if (previousop.code() != OpCode.CPUI_INDIRECT) {
                        ++iter;
                        break;
                    }
                }
            }
            opInsert(op, parent, iter);
        }

        /// Insert given PcodeOp after a specific op
        /// The given PcodeOp is inserted \e immediately after the \e prev op except:
        ///  - MULTIEQUALS in a basic block all occur first
        ///  - INDIRECTs occur immediately before their op
        ///  - a branch op must be the very last op in a basic block
        ///
        /// \param op is the given PcodeOp to insert
        /// \param prev is the op to insert after
        public void opInsertAfter(PcodeOp op, PcodeOp prev)
        {
            if (prev.isMarker()) {
                if (prev.code() == OpCode.CPUI_INDIRECT) {
                    Varnode invn = prev.getIn(1);
                    if (invn.getSpace().getType() == spacetype.IPTR_IOP) {
                        PcodeOp targOp = PcodeOp.getOpFromConst(invn.getAddr()); // Store or call
                        if (!targOp.isDead())
                            prev = targOp;
                    }
                }
            }
            IEnumerator<PcodeOp> iter = prev.getBasicIter();
            BlockBasic parent = prev.getParent();

            iter++;

            if (op.code() != OpCode.CPUI_MULTIEQUAL) {
                // There should not be a MULTIEQUAL immediately after op
                PcodeOp nextop;
                while (iter != parent.endOp()) {
                    nextop = iter.Current;
                    ++iter;
                    if (nextop.code() != OpCode.CPUI_MULTIEQUAL) {
                        --iter;
                        break;
                    }
                }
            }
            opInsert(op, prev.getParent(), iter);
        }

        /// Insert given PcodeOp at the beginning of a basic block
        /// The given PcodeOp is inserted as the \e first op in the basic block except:
        ///  - MULTIEQUALS in a basic block all occur first
        ///  - INDIRECTs occur immediately before their op
        ///  - a branch op must be the very last op in a basic block
        ///
        /// \param op is the given PcodeOp to insert
        /// \param bl is the basic block to insert into
        public void opInsertBegin(PcodeOp op, BlockBasic bl)
        {
            IEnumerator<PcodeOp> iter = bl.beginOp();

            if (op.code() != OpCode.CPUI_MULTIEQUAL) {
                while (iter.MoveNext()) {
                    if (iter.Current.code() != OpCode.CPUI_MULTIEQUAL)
                        break;
                }
            }
            opInsert(op, bl, iter);
        }

        /// Insert given PcodeOp at the end of a basic block
        /// The given PcodeOp is inserted as the \e last op in the basic block except:
        ///  - MULTIEQUALS in a basic block all occur first
        ///  - INDIRECTs occur immediately before their op
        ///  - a branch op must be the very last op in a basic block
        /// \param op is the given PcodeOp to insert
        /// \param bl is the basic block to insert into
        public void opInsertEnd(PcodeOp op, BlockBasic bl)
        {
            IBiDirEnumerator<PcodeOp> iter = bl.GetBiDirectionalEnumerator(true);

            if (!iter.MoveNext()) throw new BugException();
            if (!iter.IsBeforeFirst) {
                if (!iter.MoveNext()) throw new BugException();
                if (!iter.Current.isFlowBreak())
                    if (!iter.MoveNext()) throw new BugException();
            }
            opInsert(op, bl, iter);
        }

        /// \brief Moved given PcodeOp to specified point in the \e dead list
        public void opDeadInsertAfter(PcodeOp op, PcodeOp prev)
        {
            obank.insertAfterDead(op, prev);
        }

        /// Perform an entire heritage pass linking Varnode reads to writes
        public void opHeritage()
        {
            heritage.heritage();
        }

        /// Set the op-code for a specific PcodeOp
        /// \param op is the given PcodeOp
        /// \param opc is the op-code to set
        public void opSetOpcode(PcodeOp op, OpCode opc)
        {
#if OPACTION_DEBUG
            if (opactdbg_active)
                debugModCheck(op);
#endif
            obank.changeOpcode(op, glb.inst[opc]);
        }

        /// Mark given OpCode.CPUI_RETURN op as a \e special halt
        /// \param op is the given OpCode.CPUI_RETURN op
        /// \param flag is one of \e halt, \e badinstruction, \e unimplemented, \e noreturn, or \e missing.
        public void opMarkHalt(PcodeOp op, PcodeOp.Flags flag)
        {
            if (op.code() != OpCode.CPUI_RETURN)
                throw new LowlevelError("Only RETURN pcode ops can be marked as halt");
            flag &= (PcodeOp.Flags.halt | PcodeOp.Flags.badinstruction |
                 PcodeOp.Flags.unimplemented | PcodeOp.Flags.noreturn|
                 PcodeOp.Flags.missing);
            if (flag == 0)
                throw new LowlevelError("Bad halt flag");
            op.setFlag(flag);
        }

        /// Set a specific output Varnode for the given PcodeOp
        /// \param op is the specific PcodeOp
        /// \param vn is the output Varnode to set
        public void opSetOutput(PcodeOp op, Varnode vn)
        {
            if (vn == op.getOut()) return; // Already set to this vn
#if OPACTION_DEBUG
            if (opactdbg_active)
                debugModCheck(op);
#endif
            if (op.getOut() != (Varnode)null) {
                opUnsetOutput(op);
            }

            if (vn.getDef() != (PcodeOp)null)    // If this varnode is already an output
                opUnsetOutput(vn.getDef());
            vn = vbank.setDef(vn, op);
            setVarnodeProperties(vn);
            op.setOutput(vn);
        }

        /// Remove output Varnode from the given PcodeOp
        /// The output Varnode becomes \e free but is not immediately deleted.
        /// \param op is the given PcodeOp
        public void opUnsetOutput(PcodeOp op)
        {
            Varnode? vn = op.getOut();
            if (vn == (Varnode)null) return; // Nothing to do
#if OPACTION_DEBUG
            if (opactdbg_active)
                debugModCheck(op);
#endif
            op.setOutput((Varnode)null); // This must come before make_free
            vbank.makeFree(vn);
            vn.clearCover();
        }

        /// Set a specific input operand for the given PcodeOp
        /// \param op is the given PcodeOp
        /// \param vn is the operand Varnode to set
        /// \param slot is the input slot where the Varnode is placed
        public void opSetInput(PcodeOp op, Varnode vn, int slot)
        {
            if (vn == op.getIn(slot)) return; // Already set to this vn
            if (vn.isConstant()) {
                // Constants should have only one descendant
                if (!vn.hasNoDescend())
                    if (!vn.isSpacebase()) {
                        // Unless they are a spacebase
                        Varnode cvn = newConstant(vn.getSize(), vn.getOffset());
                        cvn.copySymbol(vn);
                        vn = cvn;
                    }
            }
#if OPACTION_DEBUG
            if (opactdbg_active)
                debugModCheck(op);
#endif
            if (op.getIn(slot) != (Varnode)null)
                opUnsetInput(op, slot);

            vn.addDescend(op);     // Add this op to list of vn's descendants
            op.setInput(vn, slot); // op must be up to date AFTER calling descend_add
        }

        /// Swap two input operands in the given PcodeOp
        /// This is convenience method that is more efficient than call opSetInput() twice.
        /// \param op is the given PcodeOp
        /// \param slot1 is the first input slot being switched
        /// \param slot2 is the second input slot
        public void opSwapInput(PcodeOp op, int slot1, int slot2)
        {
#if OPACTION_DEBUG
            if (opactdbg_active)
                debugModCheck(op);
#endif
            Varnode tmp = op.getIn(slot1);
            op.setInput(op.getIn(slot2), slot1);
            op.setInput(tmp, slot2);
        }

        /// Clear an input operand slot for the given PcodeOp
        /// The input Varnode is unlinked from the op.
        /// \param op is the given PcodeOp
        /// \param slot is the input slot to clear
        public void opUnsetInput(PcodeOp op, int slot)
        {
            Varnode vn = op.getIn(slot);

            vn.eraseDescend(op);
            op.clearInput(slot);       // Must be called AFTER descend_erase
        }

        /// \brief Insert the given PcodeOp at specific point in a basic block
        /// The PcodeOp is removed from the \e dead list and is inserted \e immediately before
        /// the specified iterator.
        /// \param op is the given PcodeOp
        /// \param bl is the basic block being inserted into
        /// \param iter indicates exactly where the op is inserted
        public void opInsert(PcodeOp op, BlockBasic bl, IEnumerator<PcodeOp> iter)
        {
#if OPACTION_DEBUG
            if (opactdbg_active)
                debugModCheck(op);
#endif
            obank.markAlive(op);
            bl.insert(iter, op);
        }

        /// Remove the given PcodeOp from its basic block
        /// The op is taken out of its basic block and put into the dead list. If the removal
        /// is permanent the input and output Varnodes should be unset.
        /// \param op is the given PcodeOp
        public void opUninsert(PcodeOp op)
        {
#if OPACTION_DEBUG
            if (opactdbg_active)
                debugModCheck(op);
#endif
            obank.markDead(op);
            op.getParent().removeOp(op);
        }

        /// Unset inputs/output and remove given PcodeOP from its basic block
        /// The op is extricated from all its Varnode connections to the functions data-flow and
        /// removed from its basic block. This will \e not change block connections.  The PcodeOp
        /// objects remains in the \e dead list.
        /// \param op is the given PcodeOp
        public void opUnlink(PcodeOp op)
        {
            int i;
#if OPACTION_DEBUG
            if (opactdbg_active)
                debugModCheck(op);
#endif
            // Unlink input and output varnodes
            opUnsetOutput(op);
            for (i = 0; i < op.numInput(); ++i)
                opUnsetInput(op, i);
            if (op.getParent() != (BlockBasic)null) // Remove us from basic block
                opUninsert(op);
        }

        /// Remove given PcodeOp and destroy its Varnode operands
        /// All input and output Varnodes to the op are destroyed (their object resources freed),
        /// and the op is permanently moved to the \e dead list.
        /// To call this routine, make sure that either:
        ///   - The op has no output
        ///   - The op's output has no descendants
        ///   - or all descendants of output are also going to be destroyed
        ///
        /// \param op is the given PcodeOp
        public void opDestroy(PcodeOp op)
        {
#if OPACTION_DEBUG
            if (opactdbg_active)
                debugModCheck(op);
#endif

            if (op.getOut() != (Varnode)null)
                destroyVarnode(op.getOut());
            for (int i = 0; i < op.numInput(); ++i) {
                Varnode? vn = op.getIn(i);
                if (vn != (Varnode)null)
                    opUnsetInput(op, i);
            }
            if (op.getParent() != (BlockBasic)null) {
                obank.markDead(op);
                op.getParent().removeOp(op);
            }
        }

        /// Remove the given \e raw PcodeOp
        /// This is a specialized routine for deleting an op during flow generation that has
        /// been replaced by something else.  The op is expected to be \e dead with none of its inputs
        /// or outputs linked to anything else.  Both the PcodeOp and all the input/output Varnodes are destroyed.
        /// \param op is the given PcodeOp
        public void opDestroyRaw(PcodeOp op)
        {
            for (int i = 0; i < op.numInput(); ++i)
                destroyVarnode(op.getIn(i));
            if (op.getOut() != (Varnode)null)
                destroyVarnode(op.getOut());
            obank.destroy(op);
        }

        /// Free resources for the given \e dead PcodeOp
        public void opDeadAndGone(PcodeOp op)
        {
            obank.destroy(op);
        }

        /// Set all input Varnodes for the given PcodeOp simultaneously
        /// All previously existing input Varnodes are unset.  The input slots for the
        /// op are resized and then filled in from the specified array.
        /// \param op is the given PcodeOp to set
        /// \param vvec is the specified array of new input Varnodes
        public void opSetAllInput(PcodeOp op, List<Varnode> vvec)
        {
            int i;

#if OPACTION_DEBUG
            if (opactdbg_active)
                debugModCheck(op);
#endif
            for (i = 0; i < op.numInput(); ++i)
                if (op.getIn(i) != (Varnode)null)
                    opUnsetInput(op, i);

            op.setNumInputs(vvec.size());

            for (i = 0; i < op.numInput(); ++i)
                opSetInput(op, vvec[i], i);
        }

        /// Remove a specific input slot for the given PcodeOp
        /// The Varnode in the specified slot is unlinked from the op and the slot itself
        /// is removed. The slot index for any remaining input Varnodes coming after the
        /// specified slot is decreased by one.
        /// \param op is the given PcodeOp
        /// \param slot is the index of the specified slot to remove
        public void opRemoveInput(PcodeOp op, int slot)
        {
#if OPACTION_DEBUG
            if (opactdbg_active)
                debugModCheck(op);
#endif
            opUnsetInput(op, slot);
            op.removeInput(slot);
        }

        /// Insert a new Varnode into the operand list for the given PcodeOp
        /// The given Varnode is set into the given operand slot. Any existing input Varnodes
        /// with slot indices equal to or greater than the specified slot are pushed into the
        /// next slot.
        /// \param op is the given PcodeOp
        /// \param vn is the given Varnode to insert
        /// \param slot is the input index to insert at
        public void opInsertInput(PcodeOp op, Varnode vn, int slot)
        {
#if OPACTION_DEBUG
            if (opactdbg_active)
                debugModCheck(op);
#endif
            op.insertInput(slot);
            opSetInput(op, vn, slot);
        }

        /// Mark PcodeOp as starting a basic block
        public void opMarkStartBasic(PcodeOp op)
        {
            op.setFlag(PcodeOp.Flags.startbasic);
        }

        /// Mark PcodeOp as starting its instruction
        public void opMarkStartInstruction(PcodeOp op)
        {
            op.setFlag(PcodeOp.Flags.startmark);
        }

        /// Mark PcodeOp as not being printed
        public void opMarkNonPrinting(PcodeOp op)
        {
            op.setFlag(PcodeOp.Flags.nonprinting);
        }

        /// Mark PcodeOp as needing special printing
        public void opMarkSpecialPrint(PcodeOp op)
        {
            op.setAdditionalFlag(PcodeOp.AdditionalFlags.special_print);
        }

        /// Mark PcodeOp as not collapsible
        public void opMarkNoCollapse(PcodeOp op)
        {
            op.setFlag(PcodeOp.Flags.nocollapse);
        }

        /// Mark cpool record was visited
        public void opMarkCpoolTransformed(PcodeOp op)
        {
            op.setAdditionalFlag(PcodeOp.AdditionalFlags.is_cpool_transformed);
        }

        /// Mark PcodeOp as having boolean output
        public void opMarkCalculatedBool(PcodeOp op)
        {
            op.setFlag(PcodeOp.Flags.calculated_bool);
        }

        /// Mark PcodeOp as LOAD/STORE from spacebase ptr
        public void opMarkSpacebasePtr(PcodeOp op)
        {
            op.setFlag(PcodeOp.Flags.spacebase_ptr);
        }

        /// Unmark PcodeOp as using spacebase ptr
        public void opClearSpacebasePtr(PcodeOp op)
        {
            op.clearFlag(PcodeOp.Flags.spacebase_ptr);
        }

        /// Flip output condition of given CBRANCH
        public void opFlipCondition(PcodeOp op)
        {
            op.flipFlag(PcodeOp.Flags.boolean_flip);
        }

        /// Look up a PcodeOp by an instruction Address
        public PcodeOp target(Address addr) => obank.target(addr);

        /// \brief Create an INT_ADD PcodeOp calculating an offset to the \e spacebase register.
        ///
        /// The \e spacebase register is looked up for the given address space, or an optional previously
        /// existing register Varnode can be provided. An insertion point op must be provided,
        /// and newly generated ops can come either before or after this insertion point.
        /// \param spc is the given address space
        /// \param off is the offset to calculate relative to the \e spacebase register
        /// \param op is the insertion point PcodeOp
        /// \param stackptr is the \e spacebase register Varnode (if available)
        /// \param insertafter is \b true if new ops are inserted \e after the insertion point
        /// \return the \e unique space Varnode holding the calculated offset
        public Varnode createStackRef(AddrSpace spc, ulong off, PcodeOp op,
            Varnode stackptr, bool insertafter)
        {
            PcodeOp addop;
            Varnode addout;
            int addrsize;

            // Calculate CURRENT stackpointer as base for relative offset
            if (stackptr == (Varnode)null)    // If we are not reusing an old reference to the stack pointer
                stackptr = newSpacebasePtr(spc); // create a new reference
            addrsize = stackptr.getSize();
            addop = newOp(2, op.getAddr());
            opSetOpcode(addop, OpCode.CPUI_INT_ADD);
            addout = newUniqueOut(addrsize, addop);
            opSetInput(addop, stackptr, 0);
            off = AddrSpace.byteToAddress(off, spc.getWordSize());
            opSetInput(addop, newConstant(addrsize, off), 1);
            if (insertafter)
                opInsertAfter(addop, op);
            else
                opInsertBefore(addop, op);

            AddrSpace containerid = spc.getContain() ?? throw new BugException();
            SegmentOp segdef = glb.userops.getSegmentOp(containerid.getIndex());

            if (segdef != (SegmentOp)null) {
                PcodeOp segop = newOp(3, op.getAddr());
                opSetOpcode(segop, OpCode.CPUI_SEGMENTOP);
                Varnode segout = newUniqueOut((int)containerid.getAddrSize(), segop);
                opSetInput(segop, newVarnodeSpace(containerid), 0);
                opSetInput(segop, newConstant(segdef.getBaseSize(), 0), 1);
                opSetInput(segop, addout, 2);
                opInsertAfter(segop, addop); // Make sure -segop- comes after -addop- regardless if before/after -op-
                addout = segout;
            }

            return addout;
        }

        /// \brief Create a LOAD expression at an offset relative to a \e spacebase register for a given address space
        ///
        /// The \e spacebase register is looked up for the given address space, or an optional previously
        /// existing register Varnode can be provided. An insertion point op must be provided,
        /// and newly generated ops can come either before or after this insertion point.
        /// \param spc is the given address space
        /// \param off is the offset to calculate relative to the \e spacebase register
        /// \param sz is the size of the desire LOAD in bytes
        /// \param op is the insertion point PcodeOp
        /// \param stackref is the \e spacebase register Varnode (if available)
        /// \param insertafter is \b true if new ops are inserted \e after the insertion point
        /// \return the \e unique space Varnode holding the result of the LOAD
        public Varnode opStackLoad(AddrSpace spc, ulong off, uint sz, PcodeOp op,
            Varnode stackref, bool insertafter)
        {
            Varnode addout = createStackRef(spc, off, op, stackref, insertafter);
            PcodeOp loadop = newOp(2, op.getAddr());
            opSetOpcode(loadop, OpCode.CPUI_LOAD);
            opSetInput(loadop, newVarnodeSpace(spc.getContain()), 0);
            opSetInput(loadop, addout, 1);
            Varnode res = newUniqueOut((int)sz, loadop);
            opInsertAfter(loadop, addout.getDef()); // LOAD comes after stack building op, regardless of -insertafter-
            return res;
        }

        /// \brief Create a STORE expression at an offset relative to a \e spacebase register for a given address space
        ///
        /// The \e spacebase register is looked up for the given address space. An insertion point
        /// op must be provided, and newly generated ops can come either before or after this insertion point.
        /// The Varnode value being stored must still be set on the returned PcodeOp.
        /// \param spc is the given address space
        /// \param off is the offset to calculate relative to the \e spacebase register
        /// \param op is the insertion point PcodeOp
        /// \param insertafter is \b true if new ops are inserted \e after the insertion point
        /// \return the STORE PcodeOp
        public PcodeOp opStackStore(AddrSpace spc, ulong off, PcodeOp op, bool insertafter)
        {
            // Create pcode sequence that stores a value at an offset relative to a spacebase
            // -off- is the offset, -size- is the size of the value
            // The sequence is inserted before/after -op- based on whether -insertafter- is false/true
            // Return the store op

            // Calculate CURRENT stackpointer as base for relative offset
            Varnode addout = createStackRef(spc, off, op, (Varnode)null, insertafter);

            PcodeOp storeop = newOp(3, op.getAddr());
            opSetOpcode(storeop, OpCode.CPUI_STORE);

            opSetInput(storeop, newVarnodeSpace(spc.getContain()), 0);
            opSetInput(storeop, addout, 1);
            opInsertAfter(storeop, addout.getDef()); // STORE comes after stack building op, regardless of -insertafter-
            return storeop;
        }

        /// Convert a OpCode.CPUI_PTRADD back into a OpCode.CPUI_INT_ADD
        /// Convert the given OpCode.CPUI_PTRADD into the equivalent OpCode.CPUI_INT_ADD.  This may involve inserting a
        /// OpCode.CPUI_INT_MULT PcodeOp. If finalization is requested and a new PcodeOp is needed, the output
        /// Varnode is marked as \e implicit and has its data-type set
        /// \param op is the given PTRADD
        /// \param finalize is \b true if finalization is needed for any new PcodeOp
        public void opUndoPtradd(PcodeOp op, bool finalize)
        {
            Varnode multVn = op.getIn(2);
            int multSize = (int)multVn.getOffset(); // Size the PTRADD thinks we are pointing

            opRemoveInput(op, 2);
            opSetOpcode(op, OpCode.CPUI_INT_ADD);
            if (multSize == 1) return;  // If no multiplier, we are done
            Varnode offVn = op.getIn(1);
            if (offVn.isConstant()) {
                ulong newVal = (uint)multSize * offVn.getOffset();
                newVal &= Globals.calc_mask((uint)offVn.getSize());
                Varnode newOffVn = newConstant(offVn.getSize(), newVal);
                if (finalize)
                    newOffVn.updateType(offVn.getTypeReadFacing(op), false, false);
                opSetInput(op, newOffVn, 1);
                return;
            }
            PcodeOp multOp = newOp(2, op.getAddr());
            opSetOpcode(multOp, OpCode.CPUI_INT_MULT);
            Varnode addVn = newUniqueOut(offVn.getSize(), multOp);
            if (finalize) {
                addVn.updateType(multVn.getType(), false, false);
                addVn.setImplied();
            }
            opSetInput(multOp, offVn, 0);
            opSetInput(multOp, multVn, 1);
            opSetInput(op, addVn, 1);
            opInsertBefore(multOp, op);
        }

        /// \brief Start of PcodeOp objects with the given op-code
        public IEnumerator<PcodeOp> beginOp(OpCode opc) => obank.begin(opc);

        ///// \brief End of PcodeOp objects with the given op-code
        //public IEnumerator<PcodeOp> endOp(OpCode opc) => obank.end(opc);

        /// \brief Start of PcodeOp objects in the \e alive list
        public IEnumerator<PcodeOp> beginOpAlive() => obank.beginAlive();

        ///// \brief End of PcodeOp objects in the \e alive list
        //public IEnumerator<PcodeOp> endOpAlive() => obank.endAlive();

        /// \brief Start of PcodeOp objects in the \e dead list
        public IEnumerator<PcodeOp> beginOpDead() => obank.beginDead();

        /// \brief End of PcodeOp objects in the \e dead list
        public IEnumerator<PcodeOp> endOpDead() => obank.endDead();

        /// \brief Start of all (alive) PcodeOp objects sorted by sequence number
        public PcodeOpTree::const_iterator beginOpAll() => obank.beginAll();

        /// \brief End of all (alive) PcodeOp objects sorted by sequence number
        public PcodeOpTree::const_iterator endOpAll() => obank.endAll();

        /// \brief Start of all (alive) PcodeOp objects attached to a specific Address
        public PcodeOpTree::const_iterator beginOp(Address addr) => obank.begin(addr);

        /// \brief End of all (alive) PcodeOp objects attached to a specific Address
        public PcodeOpTree::const_iterator endOp(Address addr) => obank.end(addr);

        /// Move given op past \e lastOp respecting covers if possible
        /// This routine should be called only after Varnode merging and explicit/implicit attributes have
        /// been calculated.  Determine if the given op can be moved (only within its basic block) to
        /// after \e lastOp.  The output of any PcodeOp moved across must not be involved, directly or
        /// indirectly, with any variable in the expression rooted at the given op.
        /// If the move is possible, perform the move.
        /// \param op is the given PcodeOp
        /// \param lastOp is the PcodeOp to move past
        /// \return \b true if the move is possible
        public bool moveRespectingCover(PcodeOp op, PcodeOp lastOp)
        {
            if (op == lastOp) return true;  // Nothing to move past
            if (op.isCall()) return false;
            PcodeOp? prevOp = (PcodeOp)null;
            if (op.code() == OpCode.CPUI_CAST) {
                Varnode vn = op.getIn(0);
                if (!vn.isExplicit()) {
                    // If CAST is part of expression, we need to move the previous op as well
                    if (!vn.isWritten()) return false;
                    prevOp = vn.getDef() ?? throw new BugException();
                    if (prevOp.isCall()) return false;
                    if (op.previousOp() != prevOp) return false;   // Previous op must exist and feed into the CAST
                }
            }
            Varnode rootvn = op.getOut();
            List<HighVariable> highList = new List<HighVariable>();
            int typeVal = HighVariable.markExpression(rootvn, highList);
            PcodeOp curOp = op;
            do {
                PcodeOp nextOp = curOp.nextOp();
                OpCode opc = nextOp.code();
                if (opc != OpCode.CPUI_COPY && opc != OpCode.CPUI_CAST) break;    // Limit ourselves to only crossing COPY and CAST ops
                if (rootvn == nextOp.getIn(0)) break;  // Data-flow order dependence
                Varnode copyVn = nextOp.getOut();
                if (copyVn.getHigh().isMark()) break; // Direct interference: COPY writes what original op reads
                if (typeVal != 0 && copyVn.isAddrTied()) break;    // Possible indirect interference
                curOp = nextOp;
            } while (curOp != lastOp);
            for (int i = 0; i < highList.size(); ++i)      // Clear marks on expression
                highList[i].clearMark();
            if (curOp == lastOp) {
                // If we are able to cross everything
                opUninsert(op);             // Move -op-
                opInsertAfter(op, lastOp);
                if (prevOp != (PcodeOp)null) {
                    // If there was a CAST, move both ops
                    opUninsert(prevOp);
                    opInsertAfter(prevOp, lastOp);
                }
                return true;
            }
            return false;
        }

        /// \brief Get the resolved union field associated with the given edge
        ///
        /// If there is no field associated with the edge, null is returned
        /// \param parent is the data-type being resolved
        /// \param op is the PcodeOp component of the given edge
        /// \param slot is the slot component of the given edge
        /// \return the associated field as a ResolvedUnion or null
        public ResolvedUnion? getUnionField(Datatype parent, PcodeOp op, int slot)
        {
            ResolveEdge edge = new ResolveEdge(parent, op, slot);
            ResolvedUnion? result;
            return unionMap.TryGetValue(edge, out result) ? result : (ResolvedUnion)null;
        }

        /// \brief Associate a union field with the given edge
        ///
        /// If there was a previous association, it is overwritten unless it was \e locked.
        /// The method returns \b true except in this case where a previous locked association exists.
        /// \param parent is the parent union data-type
        /// \param op is the PcodeOp component of the given edge
        /// \param slot is the slot component of the given edge
        /// \param resolve is the resolved union
        /// \return \b true unless there was a locked association
        public bool setUnionField(Datatype parent, PcodeOp op, int slot, ResolvedUnion resolve)
        {
            ResolveEdge edge = new ResolveEdge(parent, op, slot);
            pair<Dictionary<ResolveEdge, ResolvedUnion>::iterator, bool> res;
            res = unionMap.emplace(edge, resolve);
            if (!res.second) {
                if ((*res.first).second.isLocked()) {
                    return false;
                }
                (*res.first).second = resolve;
            }
            if (op.code() == OpCode.CPUI_MULTIEQUAL && slot >= 0) {
                // Data-type propagation doesn't happen between MULTIEQUAL input slots holding the same Varnode
                // So if this is a MULTIEQUAL, copy resolution to any other input slots holding the same Varnode
                Varnode vn = op.getIn(slot);        // The Varnode being directly set
                for (int i = 0; i < op.numInput(); ++i) {
                    if (i == slot) continue;
                    if (op.getIn(i) != vn) continue;       // Check that different input slot holds same Varnode
                    ResolveEdge dupedge = new ResolveEdge(parent, op, i);
                    res = unionMap.emplace(dupedge, resolve);
                    if (!res.second) {
                        if (!(*res.first).second.isLocked())
                            (*res.first).second = resolve;
                    }
                }
            }
            return true;
        }

        /// \brief Force a specific union field resolution for the given edge
        ///
        /// The \b parent data-type is taken directly from the given Varnode.
        /// \param parent is the parent data-type
        /// \param fieldNum is the index of the field to force
        /// \param op is PcodeOp of the edge
        /// \param slot is -1 for the write edge or >=0 indicating the particular read edge
        public void forceFacingType(Datatype parent, int fieldNum, PcodeOp op, int slot)
        {
            Datatype baseType = parent;
            if (baseType.getMetatype() == type_metatype.TYPE_PTR)
                baseType = ((TypePointer)baseType).getPtrTo();
            if (parent.isPointerRel()) {
                // Don't associate a relative pointer with the resolution, but convert to a standard pointer
                parent = glb.types.getTypePointer(parent.getSize(), baseType,
                    ((TypePointer)parent).getWordSize());
            }
            ResolvedUnion resolve = new ResolvedUnion(parent, fieldNum, glb.types);
            setUnionField(parent, op, slot, resolve);
        }

        /// \brief Copy a read/write facing resolution for a specific data-type from one PcodeOp to another
        ///
        /// \param parent is the data-type that needs resolution
        /// \param op is the new reading PcodeOp
        /// \param slot is the new slot (-1 for write, >=0 for read)
        /// \param oldOp is the PcodeOp to inherit the resolution from
        /// \param oldSlot is the old slot (-1 for write, >=0 for read)
        public int inheritResolution(Datatype parent, PcodeOp op, int slot, PcodeOp oldOp, int oldSlot)
        {
            ResolveEdge edge = new ResolveEdge(parent, oldOp, oldSlot);
            ResolvedUnion? targetUnion;
            if (unionMap.TryGetValue(edge, out targetUnion))
                return -1;
            if (null == targetUnion) throw new BugException();
            setUnionField(parent, op, slot, targetUnion);
            return targetUnion.getFieldNum();
        }

        // Jumptable routines
        /// Link jump-table with a given BRANCHIND
        /// Look up the jump-table object with the matching PcodeOp address, then
        /// attach the given PcodeOp to it.
        /// \param op is the given BRANCHIND PcodeOp
        /// \return the matching jump-table object or NULL
        public JumpTable? linkJumpTable(PcodeOp op)
        {
            foreach (JumpTable jt in jumpvec) {
                if (jt.getOpAddress() == op.getAddr()) {
                    jt.setIndirectOp(op);
                    return jt;
                }
            }
            return (JumpTable)null;
        }

        /// Find a jump-table associated with a given BRANCHIND
        /// Look up the jump-table object with the matching PcodeOp address
        /// \param op is the given BRANCHIND PcodeOp
        /// \return the matching jump-table object or NULL
        public JumpTable? findJumpTable(PcodeOp op)
        {
            foreach (JumpTable jt in jumpvec) {
                if (jt.getOpAddress() == op.getAddr()) return jt;
            }
            return (JumpTable)null;
        }

        /// Install a new jump-table for the given Address
        /// The given address must have a BRANCHIND op attached to it.
        /// This is suitable for installing an override and must be called before
        /// flow has been traced.
        /// \param addr is the given Address
        /// \return the new jump-table object
        public JumpTable installJumpTable(Address addr)
        {
            if (isProcStarted())
                throw new LowlevelError("Cannot install jumptable if flow is already traced");
            for (int i = 0; i < jumpvec.size(); ++i) {
                JumpTable jt = jumpvec[i];
                if (jt.getOpAddress() == addr)
                    throw new LowlevelError("Trying to install over existing jumptable");
            }
            JumpTable newjt = new JumpTable(glb, addr);
            jumpvec.Add(newjt);
            return newjt;
        }

        /// \brief Recover control-flow destinations for a BRANCHIND
        ///
        /// If an existing and complete JumpTable exists for the BRANCHIND, it is returned immediately.
        /// Otherwise an attempt is made to analyze the current partial function and recover the set of destination
        /// addresses, which if successful will be returned as a new JumpTable object.
        /// \param partial is the Funcdata copy to perform analysis on if necessary
        /// \param op is the given BRANCHIND PcodeOp
        /// \param flow is current flow information for \b this function
        /// \param failuremode will hold the final success/failure code (0=success)
        /// \return the recovered JumpTable or NULL if there was no success
        public JumpTable? recoverJumpTable(Funcdata partial, PcodeOp op, FlowInfo flow,
            out int failuremode)
        {
            failuremode = 0;
            JumpTable? jt = linkJumpTable(op);     // Search for pre-existing jumptable
            if (jt != (JumpTable)null) {
                if (!jt.isOverride()) {
                    if (jt.getStage() != 1)
                        return jt;      // Previously calculated jumptable (NOT an override and NOT incomplete)
                }
                failuremode = stageJumpTable(partial, jt, op, flow); // Recover based on override information
                if (failuremode != 0)
                    return (JumpTable)null;
                jt.setIndirectOp(op);  // Relink table back to original op
                return jt;
            }

            if ((flags & Flags.jumptablerecovery_dont) != 0)
                return (JumpTable)null;   // Explicitly told not to recover jumptables
            if (earlyJumpTableFail(op))
                return (JumpTable)null;
            JumpTable trialjt = new JumpTable(glb);
            failuremode = stageJumpTable(partial, trialjt, op, flow);
            if (failuremode != 0)
                return (JumpTable)null;
            //  if (trialjt.is_twostage())
            //    warning("Jumptable maybe incomplete. Second-stage recovery not implemented",trialjt.Opaddress());
            jt = new JumpTable(trialjt); // Make the jumptable permanent
            jumpvec.Add(jt);
            jt.setIndirectOp(op);      // Relink table back to original op
            return jt;
        }

        /// Try to determine, early, if jump-table analysis will fail
        /// Backtrack from the BRANCHIND, looking for ops that might affect the destination.
        /// If a CALLOTHER, which is not injected/inlined in some way, is in the flow path of
        /// the destination calculation, we know the jump-table analysis will fail and return \b true.
        /// \param op is the BRANCHIND op
        /// \return \b true if jump-table analysis is guaranteed to fail
        public bool earlyJumpTableFail(PcodeOp op)
        {
            Varnode vn = op.getIn(0);
            IEnumerator<PcodeOp> iter = op.insertiter;
            IEnumerator<PcodeOp> startiter = beginOpDead();
            int countMax = 8;
            while (iter != startiter) {
                if (vn.getSize() == 1) return false;
                countMax -= 1;
                if (countMax < 0) return false;     // Don't iterate too many times
                --iter;
                op = iter.Current;
                Varnode? outvn = op.getOut();
                bool outhit = false;
                if (outvn != (Varnode)null)
                    outhit = vn.intersects(outvn);
                if (op.getEvalType() == PcodeOp.Flags.special) {
                    if (op.isCall()) {
                        OpCode opc = op.code();
                        if (opc == OpCode.CPUI_CALLOTHER) {
                            int id = (int)op.getIn(0).getOffset();
                            UserPcodeOp userOp = glb.userops.getOp(id);
                            if (userOp is InjectedUserOp)
                                return false;   // Don't try to back track through injection
                            if (userOp is JumpAssistOp)
                                return false;
                            if (userOp is SegmentOp)
                                return false;
                            if (outhit)
                                return true;    // Address formed via uninjected CALLOTHER, analysis will fail
                                                // Assume CALLOTHER will not interfere with address and continue backtracking
                        }
                        else {
                            // CALL or CALLIND - Output has not been established yet
                            return false;   // Don't try to back track through CALL
                        }
                    }
                    else if (op.isBranch())
                        return false;   // Don't try to back track further
                    else {
                        if (op.code() == OpCode.CPUI_STORE) return false; // Don't try to back track through STORE
                        if (outhit)
                            return false;       // Some special op (CPOOLREF, NEW, etc) generates address, don't assume failure
                                                // Assume special will not interfere with address and continue backtracking
                    }
                }
                else if (op.getEvalType() == PcodeOp.Flags.unary) {
                    if (outhit) {
                        Varnode invn = op.getIn(0);
                        if (invn.getSize() != vn.getSize()) return false;
                        vn = invn;      // Treat input as address
                    }
                    // Continue backtracking
                }
                else if (op.getEvalType() == PcodeOp.Flags.binary) {
                    if (outhit) {
                        OpCode opc = op.code();
                        if (   opc != OpCode.CPUI_INT_ADD
                            && opc != OpCode.CPUI_INT_SUB
                            && opc != OpCode.CPUI_INT_XOR)
                        {
                            return false;
                        }
                        if (!op.getIn(1).isConstant()) return false;      // Don't back-track thru binary op, don't assume failure
                        Varnode invn = op.getIn(0);
                        if (invn.getSize() != vn.getSize()) return false;
                        vn = invn;      // Treat input as address
                    }
                    // Continue backtracking
                }
                else {
                    if (outhit)
                        return false;
                }
            }
            return false;
        }

        /// Get the number of jump-tables for \b this function
        public int numJumpTables() => jumpvec.size();

        /// Get the i-th jump-table
        public JumpTable getJumpTable(int i) => jumpvec[i];

        /// Remove/delete the given jump-table
        /// The JumpTable object is freed, and the associated BRANCHIND is no longer marked
        /// as a \e switch point.
        /// \param jt is the given JumpTable object
        public void removeJumpTable(JumpTable jt)
        {
            List<JumpTable> remain = new List<JumpTable>();
            IEnumerator<JumpTable> iter = jumpvec.GetEnumerator();

            while (iter.MoveNext())
                if (iter.Current != jt)
                    remain.Add(iter.Current);
            PcodeOp op = jt.getIndirectOp();
            // delete jt;
            if (op != (PcodeOp)null)
                op.getParent().clearFlag(FlowBlock.block_flags.f_switch_out);
            jumpvec = remain;
        }

        // Block routines
        /// Get the current control-flow structuring hierarchy
        public BlockGraph getStructure() => sblocks;

        /// Get the basic blocks container
        public BlockGraph getBasicBlocks() => bblocks;

        /// \brief Set the initial ownership range for the given basic block
        /// \param bb is the given basic block
        /// \param beg is the beginning Address of the owned code range
        /// \param end is the ending Address of the owned code range
        public void setBasicBlockRange(BlockBasic bb, Address beg, Address end)
        {
            bb.setInitialRange(beg, end);
        }

        /// Remove a basic block from control-flow that performs no operations
        /// The block must contain only \e marker operations (MULTIEQUAL) and possibly a single
        /// unconditional branch operation. The block and its PcodeOps are completely removed from
        /// the current control-flow and data-flow.  This forces a reset of the control-flow structuring
        /// hierarchy.
        /// \param bb is the given basic block
        public void removeDoNothingBlock(BlockBasic bb)
        {
            if (bb.sizeOut() > 1)
                throw new LowlevelError("Cannot delete a reachable block unless it has 1 out or less");

            bb.setDead();
            blockRemoveInternal(bb, false);
            structureReset();       // Delete any structure we had before
        }

        /// \brief Remove any unreachable basic blocks
        ///
        /// A quick check for unreachable blocks can optionally be made, otherwise
        /// the cached state is checked via hasUnreachableBlocks(), which is turned on
        /// during analysis by calling the structureReset() method.
        /// \param issuewarning is \b true if warning comments are desired
        /// \param checkexistence is \b true to force an active search for unreachable blocks
        /// \return \b true if unreachable blocks were actually found and removed
        public bool removeUnreachableBlocks(bool issuewarning, bool checkexistence)
        {
            List<FlowBlock> list = new List<FlowBlock>();
            int i;

            if (checkexistence) {
                // Quick check for the existence of unreachable blocks
                for (i = 0; i < bblocks.getSize(); ++i) {
                    FlowBlock blk = bblocks.getBlock(i);
                    if (blk.isEntryPoint()) continue; // Don't remove starting component
                    if (blk.getImmedDom() == (FlowBlock)null) break;
                }
                if (i == bblocks.getSize()) return false;
            }
            else if (!hasUnreachableBlocks())       // Use cached check
                return false;

            // There must be at least one unreachable block if we reach here

            for (i = 0; i < bblocks.getSize(); ++i) // Find entry point
                if (bblocks.getBlock(i).isEntryPoint()) break;
            bblocks.collectReachable(list, bblocks.getBlock(i), true); // Collect (un)reachable blocks

            for (i = 0; i < list.size(); ++i) {
                list[(int)i].setDead();
                if (issuewarning) {
                    StringWriter s = new StringWriter();
                    BlockBasic bb = (BlockBasic)list[i];
                    s.Write($"Removing unreachable block ({bb.getStart().getSpace().getName()},");
                    bb.getStart().printRaw(s);
                    s.Write(")");
                    warningHeader(s.ToString());
                }
            }
            for (i = 0; i < list.size(); ++i) {
                BlockBasic bb = (BlockBasic)list[(int)i];
                while (bb.sizeOut() > 0)
                    branchRemoveInternal(bb, 0);
            }
            for (i = 0; i < list.size(); ++i) {
                BlockBasic bb = (BlockBasic)list[(int)i];
                blockRemoveInternal(bb, true);
            }
            structureReset();
            return true;
        }

        /// \brief Move a control-flow edge from one block to another
        ///
        /// This is intended for eliminating switch guard artifacts. The edge
        /// must be for a conditional jump and must be moved to a block hosting
        /// multiple out edges for a BRANCHIND.
        /// \param bb is the basic block out of which the edge to move flows
        /// \param slot is the index of the (out) edge
        /// \param bbnew is the basic block where the edge should get moved to
        public void pushBranch(BlockBasic bb, int slot, BlockBasic bbnew)
        {
            PcodeOp cbranch = bb.lastOp() ?? throw new BugException();
            if ((cbranch.code() != OpCode.CPUI_CBRANCH) || (bb.sizeOut() != 2))
                throw new LowlevelError("Cannot push non-conditional edge");
            PcodeOp indop = bbnew.lastOp() ?? throw new BugException();
            if (indop.code() != OpCode.CPUI_BRANCHIND)
                throw new LowlevelError("Can only push branch into indirect jump");

            // Turn the conditional branch into a branch
            opRemoveInput(cbranch, 1);  // Remove the conditional variable
            opSetOpcode(cbranch, OpCode.CPUI_BRANCH);
            bblocks.moveOutEdge(bb, slot, bbnew);
            // No change needs to be made to the indirect branch
            // we assume it handles its new branch implicitly
            structureReset();
        }

        /// Remove the indicated branch from a basic block
        /// The edge is removed from control-flow and affected MULTIEQUAL ops are adjusted.
        /// \param bb is the basic block
        /// \param num is the index of the out edge to remove
        public void removeBranch(BlockBasic bb, int num)
        {
            branchRemoveInternal(bb, num);
            structureReset();
        }

        /// \brief Create a new basic block for holding a merged CBRANCH
        ///
        /// This is used by ConditionalJoin to do the low-level control-flow manipulation
        /// to merge identical conditional branches. Given basic blocks containing the two
        /// CBRANCH ops to merge, the new block gets one of the two out edges from each block,
        /// and the remaining out edges are changed to point into the new block.
        /// \param block1 is the basic block containing the first CBRANCH to merge
        /// \param block2 is the basic block containing the second CBRANCH
        /// \param exita is the first common exit block for the CBRANCHs
        /// \param exitb is the second common exit block
        /// \param fora_block1ishigh designates which edge is moved for exita
        /// \param forb_block1ishigh designates which edge is moved for exitb
        /// \param addr is the Address associated with (1 of the) CBRANCH ops
        /// \return the new basic block
        public BlockBasic nodeJoinCreateBlock(BlockBasic block1, BlockBasic block2,
            BlockBasic exita, BlockBasic exitb, bool fora_block1ishigh,
            bool forb_block1ishigh, Address addr)
        {
            BlockBasic newblock = bblocks.newBlockBasic(this);
            newblock.setFlag(FlowBlock.block_flags.f_joined_block);
            newblock.setInitialRange(addr, addr);
            FlowBlock swapa;
            FlowBlock swapb;

            // Delete 2 of the original edges into exita and exitb
            if (fora_block1ishigh) {
                // Remove the edge from block1
                bblocks.removeEdge(block1, exita);
                swapa = block2;
            }
            else {
                bblocks.removeEdge(block2, exita);
                swapa = block1;
            }
            if (forb_block1ishigh) {
                bblocks.removeEdge(block1, exitb);
                swapb = block2;
            }
            else {
                bblocks.removeEdge(block2, exitb);
                swapb = block1;
            }

            // Move the remaining two from block1,block2 to newblock
            bblocks.moveOutEdge(swapa, swapa.getOutIndex(exita), newblock);
            bblocks.moveOutEdge(swapb, swapb.getOutIndex(exitb), newblock);

            bblocks.addEdge(block1, newblock);
            bblocks.addEdge(block2, newblock);
            structureReset();
            return newblock;
        }

        /// \brief Split control-flow into a basic block, duplicating its p-code into a new block
        ///
        /// P-code is duplicated into another block, and control-flow is modified so that the new
        /// block takes over flow from one input edge to the original block.
        /// \param b is the basic block to be duplicated and split
        /// \param inedge is the index of the input edge to move to the duplicate block
        public void nodeSplit(BlockBasic b, int inedge)
        {
            // Split node b along inedge
            if (b.sizeOut() != 0)
                throw new LowlevelError("Cannot (currently) nodesplit block with out flow");
            if (b.sizeIn() <= 1)
                throw new LowlevelError("Cannot nodesplit block with only 1 in edge");
            for (int i = 0; i < b.sizeIn(); ++i) {
                if (b.getIn(i).isMark())
                    throw new LowlevelError("Cannot nodesplit block with redundant in edges");
                b.setMark();
            }
            for (int i = 0; i < b.sizeIn(); ++i)
                b.clearMark();

            // Create duplicate block
            BlockBasic bprime = nodeSplitBlockEdge(b, inedge);
            // Make copy of b's ops
            nodeSplitRawDuplicate(b, bprime);
            // Patch up inputs based on split
            nodeSplitInputPatch(b, bprime, inedge);

            // We would need to patch outputs here for the more general
            // case when b has out edges
            // any references not in b to varnodes defined in b
            // need to have MULTIEQUALs defined in b's out blocks
            //   with edges coming from b and bprime
            structureReset();
        }

        /// \brief Force a specific control-flow edge to be marked as \e unstructured
        ///
        /// The edge is specified by a source and destination Address (of the branch).
        /// The resulting control-flow structure will have a \e goto statement modeling
        /// the edge.
        /// \param pcop is the source Address
        /// \param pcdest is the destination Address
        /// \return \b true if a control-flow edge was successfully labeled
        public bool forceGoto(Address pcop, Address pcdest)
        {
            FlowBlock bl;
            FlowBlock bl2;
            PcodeOp? op;
            PcodeOp? op2;
            int i, j;

            for (i = 0; i < bblocks.getSize(); ++i) {
                bl = bblocks.getBlock(i);
                op = bl.lastOp();
                if (op == (PcodeOp)null) continue;
                if (op.getAddr() != pcop) continue;    // Find op to mark unstructured
                for (j = 0; j < bl.sizeOut(); ++j) {
                    bl2 = bl.getOut(j);
                    op2 = bl2.lastOp();
                    if (op2 == (PcodeOp)null) continue;
                    if (op2.getAddr() != pcdest) continue; // Find particular branch
                    bl.setGotoBranch(j);
                    return true;
                }
            }
            return false;
        }

        /// \brief Remove a basic block splitting its control-flow into two distinct paths
        ///
        /// This is used by ConditionalExecution to eliminate unnecessary control-flow joins.
        /// The given block must have 2 inputs and 2 outputs, (and no operations).  The block
        /// is removed, and control-flow is adjusted so that
        /// In(0) flows to Out(0) and In(1) flows to Out(1), or vice versa.
        /// \param bl is the given basic block
        /// \param swap is \b true to force In(0).Out(1) and In(1).Out(0)
        public void removeFromFlowSplit(BlockBasic bl, bool swap)
        {
            if (!bl.emptyOp())
                throw new LowlevelError("Can only split the flow for an empty block");
            bblocks.removeFromFlowSplit(bl, swap);
            bblocks.removeBlock(bl);
            structureReset();
        }

        /// \brief Switch an outgoing edge from the given \e source block to flow into another block
        ///
        /// This does \e not adjust MULTIEQUAL data-flow.
        /// \param inblock is the given \e source block
        /// \param outbefore is the other side of the desired edge
        /// \param outafter is the new destination block desired
        public void switchEdge(FlowBlock inblock, BlockBasic outbefore, FlowBlock outafter)
        {
            bblocks.switchEdge(inblock, outbefore, outafter);
            structureReset();
        }

        /// Merge the given basic block with the block it flows into
        /// The given block must have a single output block, which will be removed.  The given block
        /// has the p-code from the output block concatenated to its own, and it inherits the output
        /// block's out edges.
        /// \param bl is the given basic block
        public void spliceBlockBasic(BlockBasic bl)
        {
            BlockBasic? outbl = (BlockBasic)null;
            if (bl.sizeOut() == 1) {
                outbl = (BlockBasic)bl.getOut(0);
                if (outbl.sizeIn() != 1)
                    outbl = (BlockBasic)null;
            }
            if (outbl == (BlockBasic)null)
                throw new LowlevelError("Cannot splice basic blocks");
            // Remove any jump op at the end of -bl-
            if (!bl.op.empty()) {
                PcodeOp jumpop = bl.op.GetLastItem();
                if (jumpop.isBranch())
                    opDestroy(jumpop);
            }
            if (!outbl.op.empty()) {
                // Check for MULTIEQUALs
                PcodeOp firstop = outbl.op.First();
                if (firstop.code() == OpCode.CPUI_MULTIEQUAL)
                    throw new LowlevelError("Splicing block with MULTIEQUAL");
                firstop.clearFlag(PcodeOp.Flags.startbasic);
                IEnumerator<PcodeOp> iter = outbl.beginOp();
                // Move ops into -bl-
                while (iter.MoveNext()) {
                    PcodeOp op = iter.Current;
                    op.setParent(bl);  // Reset ops parent to -bl-
                }
                // Move all ops from -outbl- to end of -bl-
                bl.op.splice(bl.op.end(), outbl.op, outbl.op.begin(), outbl.op.end());
                // insertiter should remain valid through splice
                bl.setOrder();     // Reset the seqnum ordering on all the ops
            }
            bl.mergeRange(outbl);  // Update the address cover
            bblocks.spliceBlock(bl);
            structureReset();
        }

        /// Make sure default switch cases are properly labeled
        public void installSwitchDefaults()
        {
            IEnumerator<JumpTable> iter = jumpvec.GetEnumerator();
            while (iter.MoveNext()) {
                JumpTable jt = iter.Current;
                PcodeOp indop = jt.getIndirectOp();
                BlockBasic ind = indop.getParent();
                // Mark any switch blocks default edge
                if (jt.getDefaultBlock() != -1) // If a default case is present
                    ind.setDefaultSwitch(jt.getDefaultBlock());
            }
        }

        /// Replace INT_LESSEQUAL and INT_SLESSEQUAL expressions
        /// Do in-place replacement of
        ///   - `c <= x`   with  `c-1 < x`   OR
        ///   - `x <= c`   with  `x < c+1`
        ///
        /// \param op is comparison PcodeOp
        /// \return true if a valid replacement was performed
        public bool replaceLessequal(PcodeOp op)
        {
            Varnode vn;
            int i;
            long val, diff;

            if ((vn = op.getIn(0)).isConstant()) {
                diff = -1;
                i = 0;
            }
            else if ((vn = op.getIn(1)).isConstant()) {
                diff = 1;
                i = 1;
            }
            else
                return false;

            val = (long)vn.getOffset();  // Treat this as signed value
            Globals.sign_extend(ref val, 8 * vn.getSize() - 1);
            if (op.code() == OpCode.CPUI_INT_SLESSEQUAL) {
                if ((val < 0) && (val + diff > 0)) return false; // Check for sign overflow
                if ((val > 0) && (val + diff < 0)) return false;
                opSetOpcode(op, OpCode.CPUI_INT_SLESS);
            }
            else {
                // Check for unsigned overflow
                if ((diff == -1) && (val == 0)) return false;
                if ((diff == 1) && (val == -1)) return false;
                opSetOpcode(op, OpCode.CPUI_INT_LESS);
            }
            ulong res = (ulong)(val + diff) & Globals.calc_mask((uint)vn.getSize());
            Varnode newvn = newConstant(vn.getSize(), res);
            newvn.copySymbol(vn);  // Preserve data-type (and any Symbol info)
            opSetInput(op, newvn, i);
            return true;
        }

        /// Distribute constant coefficient to additive input
        /// If a term has a multiplicative coefficient, but the underlying term is still additive,
        /// in some situations we may need to distribute the coefficient before simplifying further.
        /// The given PcodeOp is a INT_MULT where the second input is a constant. We also
        /// know the first input is formed with INT_ADD. Distribute the coefficient to the INT_ADD inputs.
        /// \param op is the given PcodeOp
        /// \return \b true if the action was performed
        public bool distributeIntMultAdd(PcodeOp op)
        {
            Varnode newvn0;
            Varnode newvn1;
            PcodeOp addop = op.getIn(0).getDef() ?? throw new BugException();
            Varnode vn0 = addop.getIn(0);
            Varnode vn1 = addop.getIn(1);
            if ((vn0.isFree()) && (!vn0.isConstant())) return false;
            if ((vn1.isFree()) && (!vn1.isConstant())) return false;
            ulong coeff = op.getIn(1).getOffset();
            int sz = op.getOut().getSize();
            // Do distribution
            if (vn0.isConstant()) {
                ulong val = coeff * vn0.getOffset();
                val &= Globals.calc_mask((uint)sz);
                newvn0 = newConstant(sz, val);
            }
            else {
                PcodeOp newop0 = newOp(2, op.getAddr());
                opSetOpcode(newop0, OpCode.CPUI_INT_MULT);
                newvn0 = newUniqueOut(sz, newop0);
                opSetInput(newop0, vn0, 0); // To first input of original add
                Varnode newcvn = newConstant(sz, coeff);
                opSetInput(newop0, newcvn, 1);
                opInsertBefore(newop0, op);
            }

            if (vn1.isConstant()) {
                ulong val = coeff * vn1.getOffset();
                val &= Globals.calc_mask((uint)sz);
                newvn1 = newConstant(sz, val);
            }
            else {
                PcodeOp newop1 = newOp(2, op.getAddr());
                opSetOpcode(newop1, OpCode.CPUI_INT_MULT);
                newvn1 = newUniqueOut(sz, newop1);
                opSetInput(newop1, vn1, 0); // To second input of original add
                Varnode newcvn = newConstant(sz, coeff);
                opSetInput(newop1, newcvn, 1);
                opInsertBefore(newop1, op);
            }

            opSetInput(op, newvn0, 0); // new ADD's inputs are outputs of new MULTs
            opSetInput(op, newvn1, 1);
            opSetOpcode(op, OpCode.CPUI_INT_ADD);

            return true;
        }

        /// Collapse constant coefficients for two chained OpCode.CPUI_INT_MULT
        /// If:
        ///   - The given Varnode is defined by a OpCode.CPUI_INT_MULT.
        ///   - The second input to the INT_MULT is a constant.
        ///   - The first input is defined by another OpCode.CPUI_INT_MULT,
        ///   - This multiply is also by a constant.
        ///
        /// The constants are combined and \b true is returned.
        /// Otherwise no change is made and \b false is returned.
        /// \param vn is the given Varnode
        /// \return \b true if a change was made
        public bool collapseIntMultMult(Varnode vn)
        {
            if (!vn.isWritten()) return false;
            PcodeOp op = vn.getDef() ?? throw new BugException();
            if (op.code() != OpCode.CPUI_INT_MULT) return false;
            Varnode constVnFirst = op.getIn(1);
            if (!constVnFirst.isConstant()) return false;
            if (!op.getIn(0).isWritten()) return false;
            PcodeOp otherMultOp = op.getIn(0).getDef() ?? throw new BugException();
            if (otherMultOp.code() != OpCode.CPUI_INT_MULT) return false;
            Varnode constVnSecond = otherMultOp.getIn(1);
            if (!constVnSecond.isConstant()) return false;
            Varnode invn = otherMultOp.getIn(0);
            if (invn.isFree()) return false;
            int sz = invn.getSize();
            ulong val = (constVnFirst.getOffset() * constVnSecond.getOffset()) & Globals.calc_mask((uint)sz);
            Varnode newvn = newConstant(sz, val);
            opSetInput(op, newvn, 1);
            opSetInput(op, invn, 0);
            return true;
        }

        /// \brief Compare call specification objects by call site address
        /// \param a is the first call specification to compare
        /// \param b is the second call specification
        /// \return \b true if the first call specification should come before the second
        public static bool compareCallspecs(FuncCallSpecs a, FuncCallSpecs b)
        {
            int ind1, ind2;
            ind1 = a.getOp().getParent().getIndex();
            ind2 = b.getOp().getParent().getIndex();
            if (ind1 != ind2) return (ind1 < ind2);
            return (a.getOp().getSeqNum().getOrder() < b.getOp().getSeqNum().getOrder());
        }

#if OPACTION_DEBUG
        /// Hook point debugging the jump-table simplification process
        public void(*jtcallback)(Funcdata & orig, Funcdata & fd);
        /// List of modified ops
        public List<PcodeOp> modify_list;
        /// List of "before" strings for modified ops
        public List<string> modify_before;
        /// Number of debug statements printed
        public int opactdbg_count;
        /// Which debug to break on
        public int opactdbg_breakcount;
        /// Are we currently doing op action debugs
        public bool opactdbg_on;
        /// \b true if current op mods should be recorded
        public bool opactdbg_active;
        /// Has a breakpoint been hit
        public bool opactdbg_breakon;
        /// Lower bounds on the PC register
        public List<Address> opactdbg_pclow;
        /// Upper bounds on the PC register
        public List<Address> opactdbg_pchigh;
        /// Lower bounds on the unique register
        public List<uint> opactdbg_uqlow;
        /// Upper bounds on the unique register
        public Lisr<uint> opactdbg_uqhigh;

        /// Enable a debug callback
        public void enableJTCallback(void (* jtcb)(Funcdata &orig, Funcdata &fd)) { jtcallback = jtcb; }
        /// Disable debug callback
        public void disableJTCallback(void) { jtcallback = (void(*)(Funcdata & orig, Funcdata & fd))0; }
        /// Turn on recording
        public void debugActivate(void) { if (opactdbg_on) opactdbg_active = true; }
        /// Turn off recording
        public void debugDeactivate(void) { opactdbg_active = false; }

        /// Cache \e before state of the given PcodeOp
        /// The current state of the op is recorded for later comparison after
        /// its been modified.
        /// \param op is the given PcodeOp being recorded
        public void debugModCheck(PcodeOp* op)
        {
            if (op.isModified()) return;
            if (!debugCheckRange(op)) return;
            op.setAdditionalFlag(PcodeOp::modified);
            ostringstream before;
            op.printDebug(before);
            modify_list.Add(op);
            modify_before.Add( before.str() );
        }

        /// Abandon printing debug for current action
        public void debugModClear()
        {
          for(int i=0;i<modify_list.size();++i)
            modify_list[i].clearAdditionalFlag(PcodeOp::modified);
          modify_list.clear();
          modify_before.clear();
          opactdbg_active = false;
        }

        /// Print before and after strings for PcodeOps modified by given action
        /// \param actionname is the name of the Action being debugged
        public void debugModPrint(string actionname)
        {
          if (!opactdbg_active) return;
          opactdbg_active = false;
          if (modify_list.empty()) return;
          PcodeOp *op;
          ostringstream s;
          opactdbg_breakon |= (opactdbg_count == opactdbg_breakcount);

          s << "DEBUG " << dec << opactdbg_count++ << ": " << actionname << endl;
          for(int i=0;i<modify_list.size();++i) {
            op = modify_list[i];
            s << modify_before[i] << endl;
            s << "   ";
            op.printDebug(s);
            s << endl;
            op.clearAdditionalFlag(PcodeOp::modified);
          }
          modify_list.clear();
          modify_before.clear();
          glb.printDebug(s.str());
        }

        /// Has a breakpoint been hit
        public bool debugBreak(void) { return opactdbg_on&&opactdbg_breakon; }
        /// Number of code ranges being debug traced
        public int debugSize() { return opactdbg_pclow.size(); }
        /// Turn on debugging
        public void debugEnable() { opactdbg_on = true; opactdbg_count = 0; }
        /// Turn off debugging
        public void debugDisable() { opactdbg_on = false; }
        /// Clear debugging ranges
        public void debugClear()
        {
            opactdbg_pclow.clear();
            opactdbg_pchigh.clear();
            opactdbg_uqlow.clear();
            opactdbg_uqhigh.clear();
        }

        /// Check if the given PcodeOp is being debug traced
        /// \param op is the given PcodeOp to check
        /// \return \b true if the op is being traced
        public bool debugCheckRange(PcodeOp op)
        {
          int i,size;

          size = opactdbg_pclow.size();
          for(i=0;i<size;++i) {
            if (!opactdbg_pclow[i].isInvalid()) {
              if (op.getAddr() < opactdbg_pclow[i])
	        continue;
              if (opactdbg_pchigh[i] < op.getAddr())
	        continue;
            }
            if (opactdbg_uqlow[i] != uint.MaxValue) {
              if (opactdbg_uqlow[i] > op.getTime())
	        continue;
              if (opactdbg_uqhigh[i] < op.getTime())
	        continue;
            }
            return true;
          }
          return false;
        }

        /// Add a new memory range to the debug trace
        /// \param pclow is the beginning of the memory range to trace
        /// \param pchigh is the end of the range
        /// \param uqlow is an (optional) sequence number to associate with the beginning of the range
        /// \param uqhigh is an (optional) sequence number to associate with the end of the range
        public void debugSetRange(Address pclow, Address pchigh,
                   uint uqlow = uint.MaxValue, uint uqhigh = uint.MaxValue)
        {
          opactdbg_on = true;
          opactdbg_pclow.Add(pclow);
          opactdbg_pchigh.Add(pchigh);
          opactdbg_uqlow.Add(uqlow);
          opactdbg_uqhigh.Add(uqhigh);
        }

        /// Mark a breakpoint as handled
        public void debugHandleBreak() { opactdbg_breakon = false; }
        /// Break on a specific trace hit count
        public void debugSetBreak(int count) { opactdbg_breakcount = count; }

        /// Print the i-th debug trace range
        public void debugPrintRange(int i)
        {
          ostringstream s;
          if (!opactdbg_pclow[i].isInvalid()) {
            s << "PC = (";
            opactdbg_pclow[i].printRaw(s);
            s << ',';
            opactdbg_pchigh[i].printRaw(s);
            s << ")  ";
          }
          else
            s << "entire function ";
          if (opactdbg_uqlow[i] != uint.MaxValue) {
            s << "unique = (" << hex << opactdbg_uqlow[i] << ',';
            s << opactdbg_uqhigh[i] << ')';
          }
          glb.printDebug(s.str());
        }
#endif

        /// \brief Trace a boolean value to a set of PcodeOps that can be changed to flip the boolean value
        ///
        /// The boolean Varnode is either the output of the given PcodeOp or the
        /// first input if the PcodeOp is a CBRANCH. The list of ops that need flipping is
        /// returned in an array
        /// \param op is the given PcodeOp
        /// \param fliplist is the array that will hold the ops to flip
        /// \return 0 if the change normalizes, 1 if the change is ambivalent, 2 if the change does not normalize
        private static int opFlipInPlaceTest(PcodeOp op, List<PcodeOp> fliplist)
        {
            Varnode vn;
            int subtest1, subtest2;
            switch (op.code()) {
                case OpCode.CPUI_CBRANCH:
                    vn = op.getIn(1);
                    if (vn.loneDescend() != op) return 2;
                    if (!vn.isWritten()) return 2;
                    return opFlipInPlaceTest(vn.getDef(), fliplist);
                case OpCode.CPUI_INT_EQUAL:
                case OpCode.CPUI_FLOAT_EQUAL:
                    fliplist.Add(op);
                    return 1;
                case OpCode.CPUI_BOOL_NEGATE:
                case OpCode.CPUI_INT_NOTEQUAL:
                case OpCode.CPUI_FLOAT_NOTEQUAL:
                    fliplist.Add(op);
                    return 0;
                case OpCode.CPUI_INT_SLESS:
                case OpCode.CPUI_INT_LESS:
                    vn = op.getIn(0);
                    fliplist.Add(op);
                    if (!vn.isConstant()) return 1;
                    return 0;
                case OpCode.CPUI_INT_SLESSEQUAL:
                case OpCode.CPUI_INT_LESSEQUAL:
                    vn = op.getIn(1);
                    fliplist.Add(op);
                    if (vn.isConstant()) return 1;
                    return 0;
                case OpCode.CPUI_BOOL_OR:
                case OpCode.CPUI_BOOL_AND:
                    vn = op.getIn(0);
                    if (vn.loneDescend() != op) return 2;
                    if (!vn.isWritten()) return 2;
                    subtest1 = opFlipInPlaceTest(vn.getDef(), fliplist);
                    if (subtest1 == 2)
                        return 2;
                    vn = op.getIn(1);
                    if (vn.loneDescend() != op) return 2;
                    if (!vn.isWritten()) return 2;
                    subtest2 = opFlipInPlaceTest(vn.getDef(), fliplist);
                    if (subtest2 == 2)
                        return 2;
                    fliplist.Add(op);
                    return subtest1;        // Front of AND/OR must be normalizing
                default:
                    break;
            }
            return 2;
        }

        /// \brief Perform op-code flips (in-place) to change a boolean value
        ///
        /// The precomputed list of PcodeOps have their op-codes modified to
        /// facilitate the flip.
        /// \param data is the function being modified
        /// \param fliplist is the list of PcodeOps to modify
        private static void opFlipInPlaceExecute(Funcdata data, List<PcodeOp> fliplist)
        {
            Varnode vn;
            for (int i = 0; i < fliplist.size(); ++i) {
                PcodeOp op = fliplist[i];
                bool flipyes;
                OpCode opc = Globals.get_booleanflip(op.code(), out flipyes);
                if (opc == OpCode.CPUI_COPY) {
                    // We remove this (CPUI_BOOL_NEGATE) entirely
                    vn = op.getIn(0);
                    // Must be a lone descendant
                    PcodeOp otherop = op.getOut().loneDescend() ?? throw new BugException();
                    int slot = otherop.getSlot(op.getOut());
                    data.opSetInput(otherop, vn, slot); // Propagate -vn- into otherop
                    data.opDestroy(op);
                }
                else if (opc == OpCode.CPUI_MAX) {
                    if (op.code() == OpCode.CPUI_BOOL_AND)
                        data.opSetOpcode(op, OpCode.CPUI_BOOL_OR);
                    else if (op.code() == OpCode.CPUI_BOOL_OR)
                        data.opSetOpcode(op, OpCode.CPUI_BOOL_AND);
                    else
                        throw new LowlevelError("Bad flipInPlace op");
                }
                else {
                    data.opSetOpcode(op, opc);
                    if (flipyes) {
                        data.opSwapInput(op, 0, 1);
                        if ((opc == OpCode.CPUI_INT_LESSEQUAL) || (opc == OpCode.CPUI_INT_SLESSEQUAL))
                            data.replaceLessequal(op);
                    }
                }
            }
        }

        /// \brief Get the earliest use/read of a Varnode in a specified basic block
        ///
        /// \param vn is the Varnode to search for
        /// \param bl is the specified basic block in which to search
        /// \return the earliest PcodeOp reading the Varnode or NULL
        private static PcodeOp? earliestUseInBlock(Varnode vn, BlockBasic bl)
        {
            PcodeOp? res = (PcodeOp)null;
            IEnumerator<PcodeOp> iter = vn.beginDescend();
            while (iter.MoveNext()) {
                PcodeOp op = iter.Current;
                if (op.getParent() != bl) continue;
                if (res == (PcodeOp)null)
                    res = op;
                else {
                    if (op.getSeqNum().getOrder() < res.getSeqNum().getOrder())
                        res = op;
                }
            }
            return res;
        }

        /// \brief Find a duplicate calculation of a given PcodeOp reading a specific Varnode
        ///
        /// We only match 1 level of calculation.  Additionally the duplicate must occur in the
        /// indicated basic block, earlier than a specified op.
        /// \param op is the given PcodeOp
        /// \param vn is the specific Varnode that must be involved in the calculation
        /// \param bl is the indicated basic block
        /// \param earliest is the specified op to be earlier than
        /// \return the discovered duplicate PcodeOp or NULL
        private static PcodeOp cseFindInBlock(PcodeOp op, Varnode vn, BlockBasic bl, PcodeOp earliest)
        {
            IEnumerator<PcodeOp> iter = vn.beginDescend();

            while (iter.MoveNext()) {
                PcodeOp res = iter.Current;
                if (res == op) continue;    // Must not be -op-
                if (res.getParent() != bl) continue; // Must be in -bl-
                if (earliest != (PcodeOp)null) {
                    if (earliest.getSeqNum().getOrder() < res.getSeqNum().getOrder()) continue; // Must occur earlier than earliest
                }
                Varnode outvn1 = op.getOut();
                Varnode outvn2 = res.getOut();
                if (outvn2 == (Varnode)null) continue;
                Varnode[] buf1 = new Varnode[2];
                Varnode[] buf2 = new Varnode[2];
                if (functionalEqualityLevel(outvn1, outvn2, buf1, buf2) == 0)
                    return res;
            }
            return (PcodeOp)null;
        }

        /// \brief Perform a Common Subexpression Elimination step
        ///
        /// Assuming the two given PcodeOps perform the identical operation on identical operands
        /// (depth 1 functional equivalence) eliminate the redundancy.  Return the remaining (dominating)
        /// PcodeOp. If neither op dominates the other, both are eliminated, and a new PcodeOp
        /// is built at a commonly accessible point.
        /// \param data is the function being modified
        /// \param op1 is the first of the given PcodeOps
        /// \param op2 is the second given PcodeOp
        /// \return the dominating PcodeOp
        private static PcodeOp cseElimination(Funcdata data, PcodeOp op1, PcodeOp op2)
        {
            PcodeOp replace;

            if (op1.getParent() == op2.getParent()) {
                if (op1.getSeqNum().getOrder() < op2.getSeqNum().getOrder())
                    replace = op1;
                else
                    replace = op2;
            }
            else {
                BlockBasic common = (BlockBasic)FlowBlock.findCommonBlock(op1.getParent(), op2.getParent());
                if (common == op1.getParent())
                    replace = op1;
                else if (common == op2.getParent())
                    replace = op2;
                else {
                    // Neither op is ancestor of the other
                    replace = data.newOp(op1.numInput(), common.getStop());
                    data.opSetOpcode(replace, op1.code());
                    data.newVarnodeOut(op1.getOut().getSize(), op1.getOut().getAddr(), replace);
                    for (int i = 0; i < op1.numInput(); ++i) {
                        if (op1.getIn(i).isConstant())
                            data.opSetInput(replace, data.newConstant(op1.getIn(i).getSize(), op1.getIn(i).getOffset()), i);
                        else
                            data.opSetInput(replace, op1.getIn(i), i);
                    }
                    data.opInsertEnd(replace, common);
                }
            }
            if (replace != op1) {
                data.totalReplace(op1.getOut(), replace.getOut());
                data.opDestroy(op1);
            }
            if (replace != op2) {
                data.totalReplace(op2.getOut(), replace.getOut());
                data.opDestroy(op2);
            }
            return replace;
        }

        /// \brief Comparator for (hash,PcodeOp) pairs
        ///
        /// Compare by hash.
        /// \param a is the first pair
        /// \param b is the second pair
        /// \return \b true if the first comes before the second
        private static bool compareCseHash(Tuple<uint, PcodeOp> a, Tuple<uint, PcodeOp> b)
        {
            return (a.Item1 < b.Item1);
        }

        /// \brief Perform Common Subexpression Elimination on a list of Varnode descendants
        ///
        /// The list consists of PcodeOp descendants of a single Varnode paired with a hash value.
        /// The hash serves as a primary test for duplicate calculations; if it doesn't match
        /// the PcodeOps aren't common subexpressions.  This method searches for hash matches
        /// then does secondary testing and eliminates any redundancy it finds.
        /// \param data is the function being modified
        /// \param list is the list of (hash, PcodeOp) pairs
        /// \param outlist will hold Varnodes produced by duplicate calculations
        private static void cseEliminateList(Funcdata data, List<Tuple<uint, PcodeOp>> list,
            List<Varnode> outlist)
        {
            PcodeOp op1;
            PcodeOp op2;
            PcodeOp resop;
            if (list.empty()) return;
            stable_sort(list.begin(), list.end(), compareCseHash);
            IEnumerator<Tuple<uint, PcodeOp>> liter1 = list.GetEnumerator();
            IEnumerator<Tuple<uint, PcodeOp>> liter2 = list.GetEnumerator();
            if (!liter2.MoveNext()) throw new BugException();
            while (liter2.MoveNext()) {
                if (!liter1.MoveNext()) throw new BugException();
                if (liter1.Current.Item1 == liter2.Current.Item1) {
                    op1 = liter1.Current.Item2;
                    op2 = liter2.Current.Item2;
                    if ((!op1.isDead()) && (!op2.isDead()) && op1.isCseMatch(op2)) {
                        Varnode outvn1 = op1.getOut();
                        Varnode outvn2 = op2.getOut();
                        if ((outvn1 == (Varnode)null) || data.isHeritaged(outvn1)) {
                            if ((outvn2 == (Varnode)null) || data.isHeritaged(outvn2)) {
                                resop = cseElimination(data, op1, op2);
                                outlist.Add(resop.getOut());
                            }
                        }
                    }
                }
                liter1++;
            }
        }
    }
}
