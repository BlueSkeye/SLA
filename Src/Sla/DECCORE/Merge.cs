using Sla.CORE;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief Class for merging low-level Varnodes into high-level HighVariables
    ///
    /// As a node in Single Static Assignment (SSA) form, a Varnode has at most one defining
    /// operation. To get a suitable notion of a single high-level variable (HighVariable) that
    /// may be reassigned at multiple places in a single function, individual Varnode objects
    /// can be \e merged into a HighVariable object. Varnode objects may be merged in this way
    /// if there is no pairwise intersection between each Varnode's Cover, the ranges of code
    /// where the Varnode holds its value.
    ///
    /// For a given function, this class attempts to merge Varnodes using various strategies
    /// and keeps track of Cover intersections to facilitate the process. Merge strategies break up
    /// into two general categories: \b forced merges, and \b speculative merges. Forced merges
    /// \e must happen, and extra Varnodes may be added to split up problematic covers to enforce it.
    /// Forced merges include:
    ///    - Merging inputs and outputs of MULTIEQUAL and INDIRECT operations
    ///    - Merging Varnodes at global (persistent) storage locations
    ///    - Merging Varnodes at mapped stack locations
    ///
    /// Speculative merges are attempted to reduce the overall number of variables defined by a
    /// function, but any given merge attempt is abandoned if there are Cover intersections. No
    /// modification is made to the data-flow to force the merge.  Speculative merges include:
    ///   - Merging an input and output Varnode of a single p-code op
    ///   - Merging Varnodes that hold the same data-type
    internal class Merge
    {
        /// The function containing the Varnodes to be merged
        private Funcdata data;
        /// Cached intersection tests
        private HighIntersectTest testCache;
        /// COPY ops inserted to facilitate merges
        private List<PcodeOp> copyTrims;
        /// Roots of unmapped CONCAT trees
        private List<PcodeOp> protoPartial;

        /// \brief Required tests to merge HighVariables that are not Cover related
        ///
        /// This is designed to short circuit merge tests, when we know properties of the
        /// two HighVariables preclude merging. For example, you can't merge HighVariables if:
        ///   - They are locked to different data-types
        ///   - They are both mapped to different address ranges
        ///   - One is a parameter one is a global
        ///
        /// \param high_out is the first HighVariable to test
        /// \param high_in is the second HighVariable to test
        /// \return \b true if tests pass and the HighVariables are not forbidden to merge
        private static bool mergeTestRequired(HighVariable high_out, HighVariable high_in)
        {
            if (high_in == high_out) return true; // Already merged

            if (high_in.isTypeLock())  // If types are locked
                if (high_out.isTypeLock()) // dont merge unless
                    if (high_in.getType() != high_out.getType()) return false; // both types are the same

            if (high_out.isAddrTied()) {
                // Do not merge address tied input
                if (high_in.isAddrTied()) {
                    if (high_in.getTiedVarnode().getAddr() != high_out.getTiedVarnode().getAddr())
                        return false;       // with an address tied output of different address
                }
            }

            if (high_in.isInput()) {
                // Input and persist must be different vars
                // as persists inherently have their own input
                if (high_out.isPersist()) return false;
                // If we don't prevent inputs and addrtieds from
                // being merged.  Inputs can get merged with the
                // internal parts of structures on the stack
                if ((high_out.isAddrTied()) && (!high_in.isAddrTied())) return false;
            }
            else if (high_in.isExtraOut())
                return false;
            if (high_out.isInput()) {
                if (high_in.isPersist()) return false;
                if ((high_in.isAddrTied()) && (!high_out.isAddrTied())) return false;
            }
            else if (high_out.isExtraOut())
                return false;

            if (high_in.isProtoPartial()) {
                if (high_out.isProtoPartial()) return false;
                if (high_out.isInput()) return false;
                if (high_out.isAddrTied()) return false;
                if (high_out.isPersist()) return false;
            }
            if (high_out.isProtoPartial()) {
                if (high_in.isInput()) return false;
                if (high_in.isAddrTied()) return false;
                if (high_in.isPersist()) return false;
            }
            if (high_in.piece != (VariablePiece)null && high_out.piece != (VariablePiece)null) {
                VariableGroup groupIn = high_in.piece.getGroup();
                VariableGroup groupOut = high_out.piece.getGroup();
                if (groupIn == groupOut)
                    return false;
                // At least one of the pieces must represent its whole group
                if (high_in.piece.getSize() != groupIn.getSize() && high_out.piece.getSize() != groupOut.getSize())
                    return false;
            }

            Symbol symbolIn = high_in.getSymbol();
            Symbol symbolOut = high_out.getSymbol();
            if (symbolIn != (Symbol)null && symbolOut != (Symbol)null) {
                if (symbolIn != symbolOut)
                    return false;       // Map to different symbols
                if (high_in.getSymbolOffset() != high_out.getSymbolOffset())
                    return false;           // Map to different parts of same symbol
            }
            return true;
        }

        /// \brief Adjacency tests for merging Varnodes that are input or output to the same p-code op
        ///
        /// All the required tests (mergeTestRequired()) are performed, and then some additional tests
        /// are performed. This does not perform any Cover tests.
        /// \param high_out is the \e output HighVariable to test
        /// \param high_in is the \e input HighVariable to test
        /// \return \b true if tests pass and the HighVariables are not forbidden to merge
        private static bool mergeTestAdjacent(HighVariable high_out, HighVariable high_in)
        {
            if (!mergeTestRequired(high_out, high_in)) return false;

            if (high_in.isNameLock() && high_out.isNameLock())
                return false;

            // Make sure variables have the same type
            if (high_out.getType() != high_in.getType())
                return false;
            // We want to isolate the use of illegal inputs
            // as much as possible.  See we don't do any speculative
            // merges with them, UNLESS the illegal input is only
            // used indirectly
            if (high_out.isInput()) {
                Varnode vn = high_out.getInputVarnode();
                if (vn.isIllegalInput() && (!vn.isIndirectOnly())) return false;
            }
            if (high_in.isInput()) {
                Varnode vn = high_in.getInputVarnode();
                if (vn.isIllegalInput() && (!vn.isIndirectOnly())) return false;
            }
            Symbol? symbol = high_in.getSymbol();
            if (symbol != (Symbol)null)
                if (symbol.isIsolated())
                    return false;
            symbol = high_out.getSymbol();
            if (symbol != (Symbol)null)
                if (symbol.isIsolated())
                    return false;

            // Currently don't allow speculative merging of variables that are in separate overlapping collections
            if (high_out.piece != (VariablePiece)null && high_in.piece != (VariablePiece)null)
                return false;
            return true;
        }

        /// \brief Speculative tests for merging HighVariables that are not Cover related
        ///
        /// This does all the \e required and \e adjacency merge tests and then performs additional
        /// tests required for \e speculative merges.
        /// \param high_out is the first HighVariable to test
        /// \param high_in is the second HighVariable to test
        /// \return \b true if tests pass and the HighVariables are not forbidden to merge
        private static bool mergeTestSpeculative(HighVariable high_out, HighVariable high_in)
        {
            if (!mergeTestAdjacent(high_out, high_in)) return false;

            // Don't merge anything with a global speculatively
            if (high_out.isPersist()) return false;
            if (high_in.isPersist()) return false;
            // Don't merge anything speculatively with input
            if (high_out.isInput()) return false;
            if (high_in.isInput()) return false;
            // Don't merge anything speculatively with addrtied
            if (high_out.isAddrTied()) return false;
            if (high_in.isAddrTied()) return false;
            return true;
        }

        /// \brief Test if the given Varnode that \e must be merged, \e can be merged.
        ///
        /// If it cannot be merged, throw an exception.
        /// \param vn is the given Varnode
        private static void mergeTestMust(Varnode vn)
        {
            if (vn.hasCover() && !vn.isImplied())
                return;
            throw new LowlevelError("Cannot force merge of range");
        }

        /// \brief Test if the given Varnode can ever be merged.
        ///
        /// Some Varnodes (constants, annotations, implied, spacebase) are never merged with another
        /// Varnode.
        /// \param vn is the Varnode to test
        /// \return \b true if the Varnode is not forbidden from ever merging
        private static bool mergeTestBasic(Varnode vn)
        {
            if (vn == (Varnode)null) return false;
            if (!vn.hasCover()) return false;
            if (vn.isImplied()) return false;
            if (vn.isProtoPartial()) return false;
            if (vn.isSpacebase()) return false;
            return true;
        }

        /// \brief Find instance Varnodes that copied to from outside the given HighVariable
        ///
        /// Find all Varnodes in the HighVariable which are defined by a COPY from another
        /// Varnode which is \e not part of the same HighVariable.
        /// \param high is the given HighVariable
        /// \param singlelist will hold the resulting list of copied instances
        private static void findSingleCopy(HighVariable high, List<Varnode> singlelist)
        {
            int i;
            Varnode vn;
            PcodeOp op;

            for (i = 0; i < high.numInstances(); ++i) {
                vn = high.getInstance(i);
                if (!vn.isWritten()) continue;
                op = vn.getDef();
                if (op.code() != OpCode.CPUI_COPY) continue; // vn must be defineed by copy
                if (op.getIn(0).getHigh() == high) continue;  // From something NOT in same high
                singlelist.Add(vn);
            }
        }

        /// \brief Compare HighVariables by the blocks they cover
        ///
        /// This comparator sorts, based on:
        ///   - Index of the first block containing cover for the HighVariable
        ///   - Address of the first instance
        ///   - Address of the defining p-code op
        ///   - Storage address
        ///
        /// \param a is the first HighVariable to compare
        /// \param b is the second HighVariable
        /// \return \b true if the first HighVariable should be ordered before the second
        private static bool compareHighByBlock(HighVariable a, HighVariable b)
        {
            int result = a.getCover().compareTo(b.getCover());
            if (result == 0) {
                Varnode v1 = a.getInstance(0);
                Varnode v2 = b.getInstance(0);

                if (v1.getAddr() == v2.getAddr()) {
                    PcodeOp def1 = v1.getDef();
                    PcodeOp def2 = v2.getDef();
                    if (def1 == (PcodeOp)null) {
                        return def2 != (PcodeOp)null;
                    }
                    else if (def2 == (PcodeOp)null) {
                        return false;
                    }
                    return (def1.getAddr() < def2.getAddr());
                }
                return (v1.getAddr() < v2.getAddr());
            }
            return (result < 0);
        }

        /// \brief Compare COPY ops first by Varnode input, then by block containing the op
        ///
        /// A sort designed to group COPY ops from the same Varnode together. Then within a group,
        /// COPYs are sorted by their containing basic block (so that dominating ops come first).
        /// \param op1 is the first PcodeOp being compared
        /// \param op2 is the second PcodeOp being compared
        /// \return \b true if the first PcodeOp should be ordered before the second
        private static bool compareCopyByInVarnode(PcodeOp op1, PcodeOp op2)
        {
            Varnode inVn1 = op1.getIn(0);
            Varnode inVn2 = op2.getIn(0);
            if (inVn1 != inVn2)     // First compare by Varnode inputs
                return (inVn1.getCreateIndex() < inVn2.getCreateIndex());
            int index1 = op1.getParent().getIndex();
            int index2 = op2.getParent().getIndex();
            if (index1 != index2)
                return (index1 < index2);
            return (op1.getSeqNum().getOrder() < op2.getSeqNum().getOrder());
        }

        /// \brief Determine if given Varnode is shadowed by another Varnode in the same HighVariable
        ///
        /// \param vn is the Varnode to check for shadowing
        /// \return \b true if \b vn is shadowed by another Varnode in its high-level variable
        private static bool shadowedVarnode(Varnode vn)
        {
            Varnode othervn;
            HighVariable high = vn.getHigh();
            int num, i;

            num = high.numInstances();
            for (i = 0; i < num; ++i) {
                othervn = high.getInstance(i);
                if (othervn == vn) continue;
                if (vn.getCover().intersect(othervn.getCover()) == 2) return true;
            }
            return false;
        }

        /// \brief  Find all the COPY ops into the given HighVariable
        ///
        /// Collect all the COPYs whose output is the given HighVariable but
        /// the input is from a different HighVariable. Returned COPYs are sorted
        /// first by the input Varnode then by block order.
        /// \param high is the given HighVariable
        /// \param copyIns will hold the list of COPYs
        /// \param filterTemps is \b true if COPYs must have a temporary output
        private static void findAllIntoCopies(HighVariable high, List<PcodeOp> copyIns,
            bool filterTemps)
        {
            for (int i = 0; i < high.numInstances(); ++i) {
                Varnode vn = high.getInstance(i);
                if (!vn.isWritten()) continue;
                PcodeOp op = vn.getDef();
                if (op.code() != OpCode.CPUI_COPY) continue;
                if (op.getIn(0).getHigh() == high) continue;
                if (filterTemps && op.getOut().getSpace().getType() != spacetype.IPTR_INTERNAL) continue;
                copyIns.Add(op);
            }
            // Group COPYs based on the incoming Varnode then block order
            copyIns.Sort(compareCopyByInVarnode);
        }

        /// \brief Collect all instances of the given HighVariable whose Cover intersects a p-code op
        ///
        /// Efficiently test if each instance Varnodes contains the specific p-code op in its Cover
        /// and return a list of the instances that do.
        /// \param vlist will hold the resulting list of intersecting instances
        /// \param high is the given HighVariable
        /// \param op is the specific PcodeOp to test intersection with
        private void collectCovering(List<Varnode> vlist, HighVariable high, PcodeOp op)
        {
            int blk = op.getParent().getIndex();
            for (int i = 0; i < high.numInstances(); ++i) {
                Varnode vn = high.getInstance(i);
                if (vn.getCover().getCoverBlock(blk).contain(op))
                    vlist.Add(vn);
            }
        }

        /// \brief Check for for p-code op intersections that are correctable
        ///
        /// Given a list of Varnodes that intersect a specific PcodeOp, check that each intersection is
        /// on the boundary, and if so, pass back the \e read op(s) that cause the intersection.
        /// \param vlist is the given list of intersecting Varnodes
        /// \param oplist will hold the boundary intersecting \e read ops
        /// \param slotlist will hold the corresponding input slots of the instance
        /// \param op is the specific intersecting PcodeOp
        /// \return \b false if any instance in the list intersects the PcodeOp on the interior
        private bool collectCorrectable(List<Varnode> vlist, List<PcodeOp> oplist,
            List<int> slotlist, PcodeOp op)
        {
            int blk = op.getParent().getIndex();
            IEnumerator<Varnode> viter;
            IEnumerator<PcodeOp> oiter;
            Varnode vn;
            PcodeOp edgeop;
            int slot, bound;
            uint opuindex = CoverBlock.getUIndex(op);

            for (viter = vlist.begin(); viter != vlist.end(); ++viter) {
                vn = viter.Current;
                bound = vn.getCover().getCoverBlock(blk).boundary(op);
                if (bound == 0) return false;
                if (bound == 2) continue;   // Not defined before op (intersects with write op)
                for (oiter = vn.beginDescend(); oiter != vn.endDescend(); ++oiter) {
                    edgeop = oiter.Current;
                    if (CoverBlock.getUIndex(edgeop) == opuindex) {
                        // Correctable
                        oplist.Add(edgeop);
                        slot = edgeop.getSlot(vn);
                        slotlist.Add(slot);
                    }
                }
            }
            return true;
        }

        /// \brief Allocate COPY PcodeOp designed to trim an overextended Cover
        ///
        /// A COPY is allocated with the given input and data-type.  A \e unique space
        /// output is created.
        /// \param inVn is the given input Varnode for the new COPY
        /// \param addr is the address associated with the new COPY
        /// \param trimOp is an exemplar PcodeOp whose read is being trimmed
        /// \return the newly allocated COPY
        private PcodeOp allocateCopyTrim(Varnode inVn, Address addr, PcodeOp trimOp)
        {
            PcodeOp copyOp = data.newOp(1, addr);
            data.opSetOpcode(copyOp, OpCode.CPUI_COPY);
            Datatype ct = inVn.getType();
            if (ct.needsResolution()) {
                // If the data-type needs resolution
                if (inVn.isWritten()) {
                    int fieldNum = data.inheritResolution(ct, copyOp, -1, inVn.getDef(), -1);
                    data.forceFacingType(ct, fieldNum, copyOp, 0);
                }
                else {
                    int slot = trimOp.getSlot(inVn);
                    ResolvedUnion resUnion = data.getUnionField(ct, trimOp, slot);
                    int fieldNum = (resUnion == (ResolvedUnion)null) ? -1 : resUnion.getFieldNum();
                    data.forceFacingType(ct, fieldNum, copyOp, 0);
                }
            }
            Varnode outVn = data.newUnique(inVn.getSize(), ct);
            data.opSetOutput(copyOp, outVn);
            data.opSetInput(copyOp, inVn, 0);
            copyTrims.Add(copyOp);
            return copyOp;
        }

        /// \brief Snip off set of \e read p-code ops for a given Varnode
        ///
        /// The data-flow for the given Varnode is truncated by creating a COPY p-code from the Varnode
        /// into a new temporary Varnode, then replacing the Varnode reads for a specific set of
        /// p-code ops with the temporary.
        /// \param vn is the given Varnode
        /// \param markedop is the specific set of PcodeOps reading the Varnode
        private void snipReads(Varnode vn, List<PcodeOp> markedop)
        {
            if (markedop.empty()) return;

            PcodeOp copyop;
            BlockBasic bl;
            Address pc;
            PcodeOp afterop;

            // Figure out where copy is inserted
            if (vn.isInput()) {
                bl = (BlockBasic)data.getBasicBlocks().getBlock(0);
                pc = bl.getStart();
                afterop = (PcodeOp)null;
            }
            else {
                bl = vn.getDef().getParent();
                pc = vn.getDef().getAddr();
                if (vn.getDef().code() == OpCode.CPUI_INDIRECT) // snip must come after OP CAUSING EFFECT
                                                           // Not the indirect op itself
                    afterop = PcodeOp.getOpFromConst(vn.getDef().getIn(1).getAddr());
                else
                    afterop = vn.getDef();
            }
            copyop = allocateCopyTrim(vn, pc, markedop.front());
            if (afterop == (PcodeOp)null)
                data.opInsertBegin(copyop, bl);
            else
                data.opInsertAfter(copyop, afterop);

            foreach (PcodeOp op in markedop) {
                int slot = op.getSlot(vn);
                data.opSetInput(op, copyop.getOut(), slot);
            }
        }

        /// \brief Snip instances of the input of an INDIRECT op that interfere with its output
        ///
        /// Examine the input and output HighVariable for the given INDIRECT op.
        /// Varnode instances of the input that intersect the output Cover are snipped by creating
        /// a new COPY op from the input to a new temporary and then replacing the Varnode reads
        /// with the temporary.
        /// \param indop is the given INDIRECT op
        private void snipIndirect(PcodeOp indop)
        {
            PcodeOp op = PcodeOp.getOpFromConst(indop.getIn(1).getAddr()); // Indirect effect op
            List<Varnode> problemvn = new List<Varnode>();
            List<PcodeOp> correctable = new List<PcodeOp>();
            List<int> correctslot = new List<int>();
            // Collect instances of output.high that are defined
            // before (and right up to) op. These need to be snipped.
            collectCovering(problemvn, indop.getOut().getHigh(), op);
            if (problemvn.empty()) return;
            // Collect vn reads where the snip needs to be.
            // If cover properly contains op, report an error.
            // This should not be possible as that vn would have
            // to intersect with indop.output, which it is merged with.
            if (!collectCorrectable(problemvn, correctable, correctslot, op))
                throw new LowlevelError("Unable to force indirect merge");

            if (correctable.empty()) return;
            Varnode refvn = correctable.front().getIn(correctslot[0]);
            PcodeOp snipop;

            // NOTE: the covers for any input to op which is
            // an instance of the output high must
            // all intersect so the varnodes must all be
            // traceable via COPY to the same root
            snipop = allocateCopyTrim(refvn, op.getAddr(), correctable.front());
            data.opInsertBefore(snipop, op);
            int i = 0;
            foreach (PcodeOp insertop in correctable) {
                int slot = correctslot[i];
                data.opSetInput(insertop, snipop.getOut(), slot);
                if (++i >= correctslot.size()) {
                    break;
                }
            }
        }

        /// \brief Eliminate intersections of given Varnode with other Varnodes in a list
        ///
        /// Both the given Varnode and those in the list are assumed to be at the same storage address.
        /// For any intersection, identify the PcodeOp reading the given Varnode which causes the
        /// intersection and \e snip the read by inserting additional COPY ops.
        /// \param vn is the given Varnode
        /// \param blocksort is the list of other Varnodes sorted by their defining basic block
        private void eliminateIntersect(Varnode vn, List<BlockVarnode> blocksort)
        {
            List<PcodeOp> markedop = new List<PcodeOp>();
            Varnode vn2;
            int boundtype;
            int overlaptype;
            bool insertop;

            IEnumerator<PcodeOp> oiter = vn.beginDescend();
            while (oiter.MoveNext()) {
                insertop = false;
                Cover single = new Cover();
                single.addDefPoint(vn);
                PcodeOp op = oiter.Current;
                single.addRefPoint(op, vn); // Build range for a single read
                Dictionary<int, CoverBlock>.Enumerator iter = single.begin();
                while (iter.MoveNext()) {
                    int blocknum = iter.Current.Key;
                    int slot = BlockVarnode.findFront(blocknum, blocksort);
                    if (slot == -1) continue;
                    while (slot < blocksort.Count) {
                        if (blocksort[slot].getIndex() != blocknum)
                            break;
                        vn2 = blocksort[slot].getVarnode();
                        slot += 1;
                        if (vn2 == vn) continue;
                        boundtype = single.containVarnodeDef(vn2);
                        if (boundtype == 0) continue;
                        overlaptype = vn.characterizeOverlap(vn2);
                        if (overlaptype == 0) continue;     // No overlap in storage
                        if (overlaptype == 1) {
                            // Partial overlap
                            int off = (int)(vn.getOffset() - vn2.getOffset());
                            if (vn.partialCopyShadow(vn2, off))
                                continue;       // SUBPIECE shadow, not a new value
                        }
                        if (boundtype == 2) {
                            // We have to resolve things defined at same place
                            if (vn2.getDef() == (PcodeOp)null) {
                                if (vn.getDef() == (PcodeOp)null) {
                                    if (vn < vn2) continue; // Choose an arbitrary order if both are inputs
                                }
                                else
                                    continue;
                            }
                            else {
                                if (vn.getDef() != (PcodeOp)null) {
                                    if (vn2.getDef().getSeqNum().getOrder() < vn.getDef().getSeqNum().getOrder())
                                        continue;
                                }
                            }
                        }
                        else if (boundtype == 3) {
                            // intersection on the tail of the range
                          // For most operations if the READ and WRITE happen on the same op, there is really no cover
                          // intersection because the READ happens before the op and the WRITE happens after,  but
                          // if the WRITE is for an INDIRECT that is marking the READING (call) op, and the WRITE is to
                          // an address forced varnode, then because the write varnode must exist just before the op
                          // there really is an intersection.
                            if (!vn2.isAddrForce()) continue;
                            if (!vn2.isWritten()) continue;
                            PcodeOp? indop = vn2.getDef();
                            if (indop.code() != OpCode.CPUI_INDIRECT) continue;
                            // The vn2 INDIRECT must be linked to the read op
                            if (op != PcodeOp.getOpFromConst(indop.getIn(1).getAddr())) continue;
                            if (overlaptype != 1) {
                                if (vn.copyShadow(indop.getIn(0))) continue; // If INDIRECT input shadows vn, don't consider as intersection
                            }
                            else {
                                int off = (int)(vn.getOffset() - vn2.getOffset());
                                if (vn.partialCopyShadow(indop.getIn(0), off)) continue;
                            }
                        }
                        insertop = true;
                        break;          // No need to continue iterating through varnodes in block
                    }
                    if (insertop) break;    // No need to continue iterating through blocks
                }
                if (insertop)
                    markedop.Add(op);
            }
            snipReads(vn, markedop);
        }

        /// \brief Make sure all Varnodes with the same storage address and size can be merged
        ///
        /// The list of Varnodes to be merged is provided as a range in the main location sorted
        /// container.  Any discovered intersection is \b snipped by splitting data-flow for one of
        /// the Varnodes into two or more flows, which involves inserting new COPY ops and temporaries.
        /// \param startiter is the beginning of the range of Varnodes with the same storage address
        /// \param enditer is the end of the range
        private void unifyAddress(VarnodeLocSet::const_iterator startiter,
            VarnodeLocSet::const_iterator enditer)
        {
            VarnodeLocSet::const_iterator iter;
            Varnode vn;
            List<Varnode> isectlist = new List<Varnode>();
            List<BlockVarnode> blocksort = new List<BlockVarnode>();

            for (iter = startiter; iter != enditer; ++iter) {
                vn = iter.Current;
                if (vn.isFree()) continue;
                isectlist.Add(vn);
            }
            blocksort.resize(isectlist.size());
            for (int i = 0; i < isectlist.size(); ++i)
                blocksort[i].set(isectlist[i]);
            stable_sort(blocksort.begin(), blocksort.end());

            for (int i = 0; i < isectlist.size(); ++i)
                eliminateIntersect(isectlist[i], blocksort);
        }

        /// \brief Trim the output HighVariable of the given PcodeOp so that its Cover is tiny
        ///
        /// The given PcodeOp is assumed to force merging so that input and output Covers shouldn't
        /// intersect. The original PcodeOp output is \e moved so that it becomes the output of a new
        /// COPY, disassociating the original output Varnode from the inputs.
        /// \param op is the given PcodeOp
        private void trimOpOutput(PcodeOp op)
        {
            PcodeOp copyop;
            Varnode uniq;
            Varnode vn;
            PcodeOp afterop;

            if (op.code() == OpCode.CPUI_INDIRECT)
                afterop = PcodeOp.getOpFromConst(op.getIn(1).getAddr()); // Insert copyop AFTER source of indirect
            else
                afterop = op;
            vn = op.getOut();
            Datatype ct = vn.getType();
            copyop = data.newOp(1, op.getAddr());
            data.opSetOpcode(copyop, OpCode.CPUI_COPY);
            if (ct.needsResolution()) {
                int fieldNum = data.inheritResolution(ct, copyop, -1, op, -1);
                data.forceFacingType(ct, fieldNum, copyop, 0);
                if (ct.getMetatype() == type_metatype.TYPE_PARTIALUNION)
                    ct = vn.getTypeDefFacing();
            }
            uniq = data.newUnique(vn.getSize(), ct);
            data.opSetOutput(op, uniq); // Output of op is now stubby uniq
            data.opSetOutput(copyop, vn);   // Original output is bumped forward slightly
            data.opSetInput(copyop, uniq, 0);
            data.opInsertAfter(copyop, afterop);
        }

        /// \brief Trim the input HighVariable of the given PcodeOp so that its Cover is tiny
        ///
        /// The given PcodeOp is assumed to force merging so that input and output Covers shouldn't
        /// intersect. A new COPY is inserted right before the given PcodeOp with a new
        /// \e unique output that replaces the specified input, disassociating it from the
        /// other original inputs and output.
        /// \param op is the given PcodeOp
        /// \param slot is the specified slot of the input Varnode to be trimmed
        private void trimOpInput(PcodeOp op, int slot)
        {
            PcodeOp copyop;
            Varnode vn;
            Address pc;

            if (op.code() == OpCode.CPUI_MULTIEQUAL) {
                BlockBasic bb = (BlockBasic)op.getParent().getIn(slot);
                pc = bb.getStop();
            }
            else
                pc = op.getAddr();
            vn = op.getIn(slot);
            copyop = allocateCopyTrim(vn, pc, op);
            data.opSetInput(op, copyop.getOut(), slot);
            if (op.code() == OpCode.CPUI_MULTIEQUAL)
                data.opInsertEnd(copyop, (BlockBasic)op.getParent().getIn(slot));
            else
                data.opInsertBefore(copyop, op);
        }

        /// \brief Force the merge of a ranges of Varnodes with the same size and storage address
        ///
        /// The list of Varnodes to be merged is provided as a range in the main location sorted
        /// container.  Any Cover intersection is assumed to already be \b snipped, so any problems
        /// with merging cause an exception to be thrown.
        /// \param startiter is the beginning of the range of Varnodes with the same storage address
        /// \param enditer is the end of the range
        private void mergeRangeMust(VarnodeLocSet::const_iterator startiter,
            VarnodeLocSet::const_iterator enditer)
        {
            HighVariable high;
            Varnode vn;

            vn = *startiter++;
            mergeTestMust(vn);
            high = vn.getHigh();
            for (; startiter != enditer; ++startiter) {
                vn = startiter.Current;
                if (vn.getHigh() == high) continue;
                mergeTestMust(vn);
                if (!merge(high, vn.getHigh(), false))
                    throw new LowlevelError("Forced merge caused intersection");
            }
        }

        /// \brief Force the merge of all input and output Varnodes for the given PcodeOp
        ///
        /// Data-flow for specific input and output Varnodes are \e snipped until everything
        /// can be merged.
        /// \param op is the given PcodeOp
        private void mergeOp(PcodeOp op)
        {
            List<HighVariable> testlist = new List<HighVariable>();
            HighVariable high_out;
            int i, nexttrim, max;

            max = (op.code() == OpCode.CPUI_INDIRECT) ? 1 : op.numInput();
            high_out = op.getOut().getHigh();
            // First try to deal with non-cover related merge
            // restrictions
            for (i = 0; i < max; ++i) {
                HighVariable high_in = op.getIn(i).getHigh();
                if (!mergeTestRequired(high_out, high_in)) {
                    trimOpInput(op, i);
                    continue;
                }
                for (int j = 0; j < i; ++j)
                    if (!mergeTestRequired(op.getIn(j).getHigh(), high_in)) {
                        trimOpInput(op, i);
                        break;
                    }
            }
            // Now test if a merge violates cover restrictions
            mergeTest(high_out, testlist);
            for (i = 0; i < max; ++i)
                if (!mergeTest(op.getIn(i).getHigh(), testlist)) break;

            if (i != max) {
                // If there are cover restrictions
                nexttrim = 0;
                while (nexttrim < max) {
                    trimOpInput(op, nexttrim); // Trim one of the branches
                    testlist.Clear();
                    // Try the merge restriction test again
                    mergeTest(high_out, testlist);
                    for (i = 0; i < max; ++i)
                        if (!mergeTest(op.getIn(i).getHigh(), testlist)) break;
                    if (i == max) break; // We successfully test merged everything
                    nexttrim += 1;
                }
                if (nexttrim == max)    // One last trim we can try
                    trimOpOutput(op);
            }

            for (i = 0; i < max; ++i) {
                // Try to merge everything for real now
                if (!mergeTestRequired(op.getOut().getHigh(), op.getIn(i).getHigh()))
                    throw new LowlevelError("Non-cover related merge restriction violated, despite trims");
                if (!merge(op.getOut().getHigh(), op.getIn(i).getHigh(), false)) {
                    throw new LowlevelError($"Unable to force merge of op at {op.getSeqNum()}");
                }
            }
        }

        /// \brief Force the merge of all input and output Varnodes to a given INDIRECT op
        ///
        /// Merging INDIRECTs take a little care if their output is address forced because by convention
        /// the value must be present at the address BEFORE the indirect effect operation takes place.
        /// \param indop is the given INDIRECT
        private void mergeIndirect(PcodeOp indop)
        {
            Varnode outvn = indop.getOut();
            Varnode invn0 = indop.getIn(0);
            if (!outvn.isAddrForce()) {
                // If the output is NOT address forced
                mergeOp(indop);     // We can merge in the same way as a MULTIEQUAL
                return;
            }

            if (mergeTestRequired(outvn.getHigh(), invn0.getHigh())) {
                if (merge(invn0.getHigh(), outvn.getHigh(), false))
                    return;
            }
            snipIndirect(indop);        // If we cannot merge, the only thing that can go
                                        // wrong with an input trim, is if the output of
                                        // indop is involved in the input to the op causing
                                        // the indirect effect. So fix this

            PcodeOp newop = allocateCopyTrim(invn0, indop.getAddr(), indop);
            SymbolEntry entry = outvn.getSymbolEntry();
            if (entry != (SymbolEntry)null && entry.getSymbol().getType().needsResolution()) {
                data.inheritResolution(entry.getSymbol().getType(), newop, -1, indop, -1);
            }
            data.opSetInput(indop, newop.getOut(), 0);
            data.opInsertBefore(newop, indop);
            if (!mergeTestRequired(outvn.getHigh(), indop.getIn(0).getHigh()) ||
                (!merge(indop.getIn(0).getHigh(), outvn.getHigh(), false))) // Try merge again
                                                                               //  if (!merge(indop.Input(0).High(),outvn.High()))
                throw new LowlevelError("Unable to merge address forced indirect");
        }

        /// \brief Speculatively merge all HighVariables in the given list as well as possible
        ///
        /// The variables are first sorted by the index of the earliest block in their range.
        /// Then proceeding in order, an attempt is made to merge each variable with the first.
        /// The attempt fails if the \e speculative test doesn't pass or if there are Cover
        /// intersections, in which case that particular merge is skipped.
        private void mergeLinear(List<HighVariable> highvec)
        {
            List<HighVariable> highstack = new List<HighVariable>();
            IEnumerator<HighVariable> initer, outiter;
            HighVariable high;

            if (highvec.size() <= 1) return;
            foreach (HighVariable variable in highvec)
                testCache.updateHigh(variable);
            highvec.Sort(compareHighByBlock);
            for (initer = highvec.begin(); initer != highvec.end(); ++initer)
            {
                high = *initer;
                for (outiter = highstack.begin(); outiter != highstack.end(); ++outiter)
                {
                    if (mergeTestSpeculative(*outiter, high))
                        if (merge(*outiter, high, true)) break;
                }
                if (outiter == highstack.end())
                    highstack.Add(high);
            }
        }

        private bool merge(HighVariable high1, HighVariable high2, bool isspeculative)
        {
            if (high1 == high2) return true; // Already merged
            if (testCache.intersection(high1, high2)) return false;

            high1.merge(high2, testCache, isspeculative); // Do the actual merge
            high1.updateCover();               // Update cover now so that updateHigh won't purge cached tests

            return true;
        }

        /// \brief Check if the given PcodeOp COPYs are redundant
        ///
        /// Both the given COPYs assign to the same HighVariable. One is redundant if there is no other
        /// assignment to the HighVariable between the first COPY and the second COPY.
        /// The first COPY must come from a block with a smaller or equal index to the second COPY.
        /// If the indices are equal, the first COPY must come before the second within the block.
        /// \param high is the HighVariable being assigned to
        /// \param domOp is the first COPY
        /// \param subOp is the second COPY
        /// \return \b true if the second COPY is redundant
        private bool checkCopyPair(HighVariable high, PcodeOp domOp, PcodeOp subOp)
        {
            FlowBlock domBlock = domOp.getParent();
            FlowBlock subBlock = subOp.getParent();
            if (!domBlock.dominates(subBlock))
                return false;
            Cover range = new Cover();
            range.addDefPoint(domOp.getOut());
            range.addRefPoint(subOp, subOp.getIn(0));
            Varnode inVn = domOp.getIn(0);
            // Look for high Varnodes in the range
            for (int i = 0; i < high.numInstances(); ++i) {
                Varnode vn = high.getInstance(i);
                if (!vn.isWritten()) continue;
                PcodeOp op = vn.getDef();
                if (op.code() == OpCode.CPUI_COPY) {
                    // If the write is not a COPY
                    if (op.getIn(0) == inVn) continue; // from the same Varnode as domOp and subOp
                }
                if (range.contain(op, 1)) {
                    // and if write is contained in range between domOp and subOp
                    return false;               // it is intervening and subOp is not redundant
                }
            }
            return true;
        }

        /// \brief Try to replace a set of COPYs from the same Varnode with a single dominant COPY
        ///
        /// All the COPY outputs must be instances of the same HighVariable (not the same Varnode).
        /// Either an existing COPY dominates all the others, or a new dominating COPY is constructed.
        /// The read locations of all other COPY outputs are replaced with the output of the dominating
        /// COPY, if it does not cause intersections in the HighVariable's Cover. Because of
        /// intersections, replacement may fail or partially succeed. Replacement only happens with
        /// COPY outputs that are temporary registers. The cover of the HighVariable may be extended
        /// because of a new COPY output instance.
        /// \param high is the HighVariable being copied to
        /// \param copy is the list of COPY ops into the HighVariable
        /// \param pos is the index of the first COPY from the specific input Varnode
        /// \param size is the number of COPYs (in sequence) from the same specific Varnode
        private void buildDominantCopy(HighVariable high, List<PcodeOp> copy, int pos, int size)
        {
            List<FlowBlock> blockSet = new List<FlowBlock>();
            for (int i = 0; i < size; ++i)
                blockSet.Add(copy[pos + i].getParent());
            BlockBasic domBl = (BlockBasic)FlowBlock.findCommonBlock(blockSet);
            PcodeOp domCopy = copy[pos];
            Varnode rootVn = domCopy.getIn(0);
            Varnode domVn = domCopy.getOut();
            bool domCopyIsNew;
            if (domBl == domCopy.getParent()) {
                domCopyIsNew = false;
            }
            else {
                domCopyIsNew = true;
                PcodeOp oldCopy = domCopy;
                domCopy = data.newOp(1, domBl.getStop());
                data.opSetOpcode(domCopy, OpCode.CPUI_COPY);
                Datatype ct = rootVn.getType();
                if (ct.needsResolution()) {
                    ResolvedUnion resUnion = data.getUnionField(ct, oldCopy, 0);
                    int fieldNum = (resUnion == (ResolvedUnion)null) ? -1 : resUnion.getFieldNum();
                    data.forceFacingType(ct, fieldNum, domCopy, 0);
                    data.forceFacingType(ct, fieldNum, domCopy, -1);
                    if (ct.getMetatype() == type_metatype.TYPE_PARTIALUNION)
                        ct = rootVn.getTypeReadFacing(oldCopy);
                }
                domVn = data.newUnique(rootVn.getSize(), ct);
                data.opSetOutput(domCopy, domVn);
                data.opSetInput(domCopy, rootVn, 0);
                data.opInsertEnd(domCopy, domBl);
            }
            // Cover created by removing all the COPYs from rootVn
            Cover bCover;
            for (int i = 0; i < high.numInstances(); ++i) {
                Varnode vn = high.getInstance(i);
                if (vn.isWritten()) {
                    PcodeOp op = vn.getDef();
                    if (op.code() == OpCode.CPUI_COPY) {
                        if (op.getIn(0).copyShadow(rootVn)) continue;
                    }
                }
                bCover.merge(vn.getCover());
            }

            int count = size;
            for (int i = 0; i < size; ++i) {
                PcodeOp op = copy[pos + i];
                if (op == domCopy) continue;    // No intersections from domVn already proven
                Varnode outVn = op.getOut();
                Cover aCover = new Cover();
                aCover.addDefPoint(domVn);
                IEnumerator<PcodeOp> iter = outVn.beginDescend();
                while (iter.MoveNext())
                    aCover.addRefPoint(iter.Current, outVn);
                if (bCover.intersect(aCover) > 1) {
                    count -= 1;
                    op.setMark();
                }
            }

            if (count <= 1) {
                // Don't bother if we only replace one COPY with another
                for (int i = 0; i < size; ++i)
                    copy[pos + i].setMark();
                count = 0;
                if (domCopyIsNew) {
                    data.opDestroy(domCopy);
                }
            }
            // Replace all non-intersecting COPYs with read of dominating Varnode
            for (int i = 0; i < size; ++i) {
                PcodeOp op = copy[pos + i];
                if (op.isMark())
                    op.clearMark();
                else {
                    Varnode outVn = op.getOut();
                    if (outVn != domVn) {
                        outVn.getHigh().remove(outVn);
                        data.totalReplace(outVn, domVn);
                        data.opDestroy(op);
                    }
                }
            }
            if (count > 0 && domCopyIsNew) {
                high.merge(domVn.getHigh(), (HighIntersectTest)null, true);
            }
        }

        /// \brief Search for and mark redundant COPY ops into the given high as \e non-printing
        ///
        /// Trimming during the merge process can insert multiple COPYs from the same source. In some
        /// cases, one or more COPYs may be redundant and shouldn't be printed. This method searches
        /// for redundancy among COPY ops that assign to the given HighVariable.
        /// \param high is the given HighVariable
        /// \param copy is the list of COPYs coming from the same source HighVariable
        /// \param pos is the starting index of a set of COPYs coming from the same Varnode
        /// \param size is the number of Varnodes in the set coming from the same Varnode
        private void markRedundantCopies(HighVariable high, List<PcodeOp> copy, int pos, int size)
        {
            for (int i = size - 1; i > 0; --i) {
                PcodeOp subOp = copy[pos + i];
                if (subOp.isDead()) continue;
                for (int j = i - 1; j >= 0; --j) {
                    // Make sure earlier index provides dominant op
                    PcodeOp domOp = copy[pos + j];
                    if (domOp.isDead()) continue;
                    if (checkCopyPair(high, domOp, subOp)) {
                        data.opMarkNonPrinting(subOp);
                        break;
                    }
                }
            }
        }

        /// \brief Try to replace COPYs into the given HighVariable with a single dominant COPY
        ///
        /// Find groups of COPYs into the given HighVariable that come from a single source Varnode,
        /// then try to replace them with a COPY.
        /// \param high is the given HighVariable
        private void processHighDominantCopy(HighVariable high)
        {
            List<PcodeOp> copyIns = new List<PcodeOp>();

            findAllIntoCopies(high, copyIns, true); // Get all COPYs into this with temporary output
            if (copyIns.size() < 2) return;
            int pos = 0;
            while (pos < copyIns.size()) {
                // Find a group of COPYs coming from the same Varnode
                Varnode inVn = copyIns[pos].getIn(0);
                int sz = 1;
                while (pos + sz < copyIns.size()) {
                    Varnode nextVn = copyIns[pos + sz].getIn(0);
                    if (nextVn != inVn) break;
                    sz += 1;
                }
                if (sz > 1)     // If there is more than one COPY in a group
                    buildDominantCopy(high, copyIns, pos, sz);  // Try to construct a dominant COPY
                pos += sz;
            }
        }

        /// \brief Mark COPY ops into the given HighVariable that are redundant
        ///
        /// A COPY is redundant if another COPY performs the same action and has
        /// dominant control flow. The redundant COPY is not removed but is marked so that
        /// it doesn't print in the final source output.
        /// \param high is the given HighVariable
        private void processHighRedundantCopy(HighVariable high)
        {
            List<PcodeOp> copyIns = new List<PcodeOp>();

            findAllIntoCopies(high, copyIns, false);
            if (copyIns.size() < 2) return;
            int pos = 0;
            while (pos < copyIns.size()) {
                // Find a group of COPYs coming from the same Varnode
                Varnode inVn = copyIns[pos].getIn(0);
                int sz = 1;
                while (pos + sz < copyIns.size()) {
                    Varnode nextVn = copyIns[pos + sz].getIn(0);
                    if (nextVn != inVn) break;
                    sz += 1;
                }
                if (sz > 1) {
                    // If there is more than one COPY in a group
                    markRedundantCopies(high, copyIns, pos, sz);
                }
                pos += sz;
            }
        }

        /// \brief Group the different nodes of a CONCAT tree into a VariableGroup
        ///
        /// This formally labels all the Varnodes in the tree as overlapping pieces of the same variable.
        /// The tree is reconstructed from the root Varnode.
        /// \param vn is the root Varnode
        private void groupPartialRoot(Varnode vn)
        {
            HighVariable high = vn.getHigh();
            if (high.numInstances() != 1) return;
            List<PieceNode> pieces;

            int baseOffset = 0;
            SymbolEntry entry = vn.getSymbolEntry();
            if (entry != (SymbolEntry)null) {
                baseOffset = entry.getOffset();
            }

            PieceNode.gatherPieces(pieces, vn, vn.getDef(), baseOffset);
            bool throwOut = false;
            for (int i = 0; i < pieces.size(); ++i) {
                Varnode nodeVn = pieces[i].getVarnode();
                // Make sure each node is still marked and hasn't merged with anything else
                if (!nodeVn.isProtoPartial() || nodeVn.getHigh().numInstances() != 1) {
                    throwOut = true;
                    break;
                }
            }
            if (throwOut) {
                for (int i = 0; i < pieces.size(); ++i)
                    pieces[i].getVarnode().clearProtoPartial();
            }
            else {
                for (int i = 0; i < pieces.size(); ++i) {
                    Varnode nodeVn = pieces[i].getVarnode();
                    nodeVn.getHigh().groupWith(pieces[i].getTypeOffset() - baseOffset, high);
                }
            }
        }

        /// Construct given a specific function
        public Merge(Funcdata fd)
        {
            data = fd;
        }

        /// \brief Clear the any cached data from the last merge process
        ///
        /// Free up resources used by cached intersection tests etc.
        public void clear()
        {
            testCache.clear();
            copyTrims.Clear();
            protoPartial.Clear();
        }

        /// \brief Test if we can inflate the Cover of the given Varnode without incurring intersections
        ///
        /// This routine tests whether an expression involving a HighVariable can be propagated to all
        /// the read sites of the output Varnode of the expression. This is possible only if the
        /// Varnode Cover can be \b inflated to include the Cover of the HighVariable, even though the
        /// Varnode is not part of the HighVariable.
        /// \param a is the given Varnode to inflate
        /// \param high is the HighVariable being propagated
        /// \return \b true if inflating the Varnode causes an intersection
        public bool inflateTest(Varnode a, HighVariable high)
        {
            HighVariable ahigh = a.getHigh();

            testCache.updateHigh(high);
            Cover highCover = high.internalCover;    // Only check for intersections with cover contributing to inflate

            for (int i = 0; i < ahigh.numInstances(); ++i) {
                Varnode b = ahigh.getInstance(i);
                if (b.copyShadow(a)) continue;     // Intersection with a or shadows of a is allowed
                if (2 == b.getCover().intersect(highCover))
                {
                    return true;
                }
            }
            VariablePiece? piece = ahigh.piece;
            if (piece != (VariablePiece)null) {
                piece.updateIntersections();
                for (int i = 0; i < piece.numIntersection(); ++i) {
                    VariablePiece otherPiece = piece.getIntersection(i);
                    HighVariable otherHigh = otherPiece.getHigh();
                    int off = otherPiece.getOffset() - piece.getOffset();
                    for (int i = 0; i < otherHigh.numInstances(); ++i) {
                        Varnode b = otherHigh.getInstance(i);
                        if (b.partialCopyShadow(a, off)) continue; // Intersection with partial shadow of a is allowed
                        if (2 == b.getCover().intersect(highCover))
                            return true;
                    }
                }
            }
            return false;
        }

        /// \brief Inflate the Cover of a given Varnode with a HighVariable
        ///
        /// An expression involving a HighVariable can be propagated to all the read sites of the
        /// output Varnode of the expression if the Varnode Cover can be \b inflated to include the
        /// Cover of the HighVariable, even though the Varnode is not part of the HighVariable.
        /// This routine performs the inflation, assuming an intersection test is already performed.
        /// \param a is the given Varnode to inflate
        /// \param high is the HighVariable to inflate with
        public void inflate(Varnode a, HighVariable high)
        {
            testCache.updateHigh(a.getHigh());
            testCache.updateHigh(high);
            for (int i = 0; i < high.numInstances(); ++i) {
                Varnode b = high.getInstance(i);
                a.cover.merge(b.cover);
            }
            a.getHigh().coverDirty();
        }

        /// \brief Test for intersections between a given HighVariable and a list of other HighVariables
        ///
        /// If there is any Cover intersection between the given HighVariable and one in the list,
        /// this routine returns \b false.  Otherwise, the given HighVariable is added to the end of
        /// the list and \b true is returned.
        /// \param high is the given HighVariable
        /// \param tmplist is the list of HighVariables to test against
        /// \return \b true if there are no pairwise intersections.
        public bool mergeTest(HighVariable high, List<HighVariable> tmplist)
        {
            if (!high.hasCover()) return false;

            for (int i = 0; i < tmplist.size(); ++i) {
                HighVariable a = tmplist[i];
                if (testCache.intersection(a, high))
                    return false;
            }
            tmplist.Add(high);
            return true;
        }

        /// \brief Try to force merges of input to output for all p-code ops of a given type
        ///
        /// For a given opcode, run through all ops in the function in block/address order and
        /// try to merge each input HighVariable with the output HighVariable.  If this would
        /// introduce Cover intersections, the merge is skipped.  This is generally used to try to
        /// merge the input and output of COPY ops if possible.
        /// \param opc is the op-code type to merge
        public void mergeOpcode(OpCode opc)
        {
            BlockBasic bl;
            IEnumerator<PcodeOp> iter;
            PcodeOp op;
            Varnode vn1;
            Varnode vn2;
            BlockGraph bblocks = data.getBasicBlocks();

            for (int i = 0; i < bblocks.getSize(); ++i) {
                // Do merges in linear block order
                bl = (BlockBasic)bblocks.getBlock(i);
                for (iter = bl.beginOp(); iter != bl.endOp(); ++iter) {
                    op = iter.Current;
                    if (op.code() != opc) continue;
                    vn1 = op.getOut();
                    if (!mergeTestBasic(vn1)) continue;
                    for (int j = 0; j < op.numInput(); ++j) {
                        vn2 = op.getIn(j);
                        if (!mergeTestBasic(vn2)) continue;
                        if (mergeTestRequired(vn1.getHigh(), vn2.getHigh()))
                            merge(vn1.getHigh(), vn2.getHigh(), false);   // This is a required merge
                    }
                }
            }
        }

        /// \brief Try to merge all HighVariables in the given range that have the same data-type
        ///
        /// HighVariables that have an instance within the given Varnode range are sorted into groups
        /// based on their data-type.  Then an attempt is made to merge all the HighVariables within
        /// a group. If a particular merge causes Cover intersection, it is skipped.
        /// \param startiter is the start of the given range of Varnodes
        /// \param enditer is the end of the given range
        public void mergeByDatatype(VarnodeLocSet::const_iterator startiter,
            VarnodeLocSet::const_iterator enditer)
        {
            List<HighVariable> highvec = new List<HighVariable>();
            List<HighVariable> highlist = new List<HighVariable>();

            IEnumerator<HighVariable> hiter;
            VarnodeLocSet::const_iterator iter;
            Varnode vn;
            HighVariable high;
            Datatype? ct = (Datatype)null;

            for (iter = startiter; iter != enditer; ++iter) {
                // Gather all the highs
                vn = iter.Current;
                if (vn.isFree()) continue;
                high = iter.Current.getHigh();
                if (high.isMark()) continue;   // dedup
                if (!mergeTestBasic(vn)) continue;
                high.setMark();
                highlist.Add(high);
            }
            for (hiter = highlist.begin(); hiter != highlist.end(); ++hiter)
                hiter.Current.clearMark();

            while (!highlist.empty()) {
                highvec.Clear();
                hiter = highlist.begin();
                high = *hiter;
                ct = high.getType();
                highvec.Add(high);
                highlist.erase(hiter++);
                while (hiter != highlist.end()) {
                    high = hiter.Current;
                    if (ct == high.getType()) {
                        // Check for exact same type
                        highvec.Add(high);
                        highlist.erase(hiter++);
                    }
                    else
                        ++hiter;
                }
                mergeLinear(highvec);   // Try to merge all highs of the same type
            }
        }

        /// \brief Force the merge of \e address \e tried Varnodes
        ///
        /// For each set of address tied Varnodes with the same size and storage address, merge
        /// them into a single HighVariable. The merges are \e forced, so any Cover intersections must
        /// be resolved by altering data-flow, which involves inserting additional COPY ops and
        /// \e unique Varnodes.
        public void mergeAddrTied()
        {
            VarnodeLocSet::const_iterator startiter;
            List<VarnodeLocSet::const_iterator> bounds;
            for (startiter = data.beginLoc(); startiter != data.endLoc();) {
                AddrSpace spc = (*startiter).getSpace();
                spacetype type = spc.getType();
                if (type != spacetype.IPTR_PROCESSOR && type != spacetype.IPTR_SPACEBASE) {
                    startiter = data.endLoc(spc);   // Skip over the whole space
                    continue;
                }
                VarnodeLocSet::const_iterator finaliter = data.endLoc(spc);
                while (startiter != finaliter) {
                    Varnode vn = *startiter;
                    if (vn.isFree()) {
                        startiter = data.endLoc(vn.getSize(), vn.getAddr(), 0);   // Skip over any free Varnodes
                        continue;
                    }
                    bounds.Clear();
                    Varnode.varnode_flags flags = data.overlapLoc(startiter, bounds);   // Collect maximally overlapping range of Varnodes
                    int max = bounds.size() - 1;           // Index of last iterator
                    if ((flags & Varnode.varnode_flags.addrtied) != 0) {
                        unifyAddress(startiter, bounds[max]);
                        for (int i = 0; i < max; i += 2) {           // Skip last iterator
                            mergeRangeMust(bounds[i], bounds[i + 1]);
                        }
                        if (max > 2) {
                            Varnode vn1 = *bounds[0];
                            for (int i = 2; i < max; i += 2) {
                                Varnode vn2 = *bounds[i];
                                int off = (int)(vn2.getOffset() - vn1.getOffset());
                                vn2.getHigh().groupWith(off, vn1.getHigh());
                            }
                        }
                    }
                    startiter = bounds[max];
                }
            }
        }

        /// \brief Force the merge of input and output Varnodes to MULTIEQUAL and INDIRECT ops
        ///
        /// Run through all MULTIEQUAL and INDIRECT ops in the function. Force the merge of each
        /// input Varnode with the output Varnode, doing data-flow modification if necessary to
        /// resolve Cover intersections.
        public void mergeMarker()
        {
            IEnumerator<PcodeOp> iter = data.beginOpAlive();
            while (iter.MoveNext()) {
                PcodeOp op = iter.Current;
                if ((!op.isMarker()) || op.isIndirectCreation()) continue;
                if (op.code() == OpCode.CPUI_INDIRECT)
                    mergeIndirect(op);
                else
                    mergeOp(op);
            }
        }

        /// \brief Run through CONCAT tree roots and group each tree
        ///
        public void groupPartials()
        {
            for (int i = 0; i < protoPartial.size(); ++i) {
                PcodeOp op = protoPartial[i];
                if (op.isDead()) continue;
                if (!op.isPartialRoot()) continue;
                groupPartialRoot(op.getOut());
            }
        }

        /// \brief Speculatively merge Varnodes that are input/output to the same p-code op
        ///
        /// If a single p-code op has an input and output HighVariable that share the same data-type,
        /// attempt to merge them. Each merge is speculative and is skipped if it would introduce Cover
        /// intersections.
        public void mergeAdjacent()
        {
            PcodeOp op;
            int i;
            HighVariable high_in, high_out;
            Varnode vn1, vn2;
            Datatype ct;

            IEnumerator<PcodeOp> oiter = data.beginOpAlive();
            while (oiter.MoveNext()) {
                op = oiter.Current;
                if (op.isCall()) continue;
                vn1 = op.getOut();
                if (!mergeTestBasic(vn1)) continue;
                high_out = vn1.getHigh();
                ct = op.outputTypeLocal();
                for (i = 0; i < op.numInput(); ++i) {
                    if (ct != op.inputTypeLocal(i)) continue; // Only merge if types should be the same
                    vn2 = op.getIn(i);
                    if (!mergeTestBasic(vn2)) continue;
                    if (vn1.getSize() != vn2.getSize()) continue;
                    if ((vn2.getDef() == (PcodeOp)null) && (!vn2.isInput())) continue;
                    high_in = vn2.getHigh();
                    if (!mergeTestAdjacent(high_out, high_in)) continue;

                    if (!testCache.intersection(high_in, high_out)) // If no interval intersection
                        merge(high_out, high_in, true);
                }
            }
        }

        /// \brief Merge together Varnodes mapped to SymbolEntrys from the same Symbol
        ///
        /// Symbols that have more than one SymbolEntry may attach to more than one Varnode.
        /// These Varnodes need to be merged to properly represent a single variable.
        public void mergeMultiEntry()
        {
            SymbolNameTree::const_iterator iter = data.getScopeLocal().beginMultiEntry();
            SymbolNameTree::const_iterator enditer = data.getScopeLocal().endMultiEntry();
            for (; iter != enditer; ++iter) {
                List<Varnode> mergeList = new List<Varnode>();
                Symbol symbol = *iter;
                int numEntries = symbol.numEntries();
                int mergeCount = 0;
                int skipCount = 0;
                int conflictCount = 0;
                for (int i = 0; i < numEntries; ++i) {
                    int prevSize = mergeList.size();
                    SymbolEntry entry = symbol.getMapEntry(i);
                    if (entry.getSize() != symbol.getType().getSize())
                        continue;
                    data.findLinkedVarnodes(entry, mergeList);
                    if (mergeList.size() == prevSize)
                        skipCount += 1;     // Did not discover any Varnodes corresponding to a particular SymbolEntry
                }
                if (mergeList.empty()) continue;
                HighVariable high = mergeList[0].getHigh();
                testCache.updateHigh(high);
                for (int i = 0; i < mergeList.size(); ++i) {
                    HighVariable newHigh = mergeList[i].getHigh();
                    if (newHigh == high) continue;      // Varnodes already merged
                    testCache.updateHigh(newHigh);
                    if (!mergeTestRequired(high, newHigh)) {
                        symbol.setMergeProblems();
                        newHigh.setUnmerged();
                        conflictCount += 1;
                        continue;
                    }
                    if (!merge(high, newHigh, false)) {
                        // Attempt the merge
                        symbol.setMergeProblems();
                        newHigh.setUnmerged();
                        conflictCount += 1;
                        continue;
                    }
                    mergeCount += 1;
                }
                if (skipCount != 0 || conflictCount != 0) {
                    TextWriter s = new StringWriter();
                    s.Write("Unable to");
                    if (mergeCount != 0)
                        s.Write(" fully");
                    s.Write($" merge symbol: {symbol.getName()}");
                    if (skipCount > 0)
                        s.Write(" -- Some instance varnodes not found.");
                    if (conflictCount > 0)
                        s.Write(" -- Some merges are forbidden");
                    data.warningHeader(s.ToString());
                }
            }
        }

        /// \brief Hide \e shadow Varnodes related to the given HighVariable by consolidating COPY chains
        ///
        /// If two Varnodes are copied from the same common ancestor then they will always contain the
        /// same value and can be considered \b shadows of the same variable.  If the paths from the
        /// ancestor to the two Varnodes aren't properly nested, the two Varnodes will still look like
        /// distinct variables.  This routine searches for this situation, relative to a single
        /// HighVariable, and alters data-flow so that copying from ancestor to first Varnode to
        /// second Varnode becomes a single path. Both Varnodes then ultimately become instances of the
        /// same HighVariable.
        /// \param high is the given HighVariable to search near
        /// \return \b true if a change was made to data-flow
        public bool hideShadows(HighVariable high)
        {
            List<Varnode> singlelist = new List<Varnode>();
            int i, j;
            bool res = false;

            findSingleCopy(high, singlelist); // Find all things copied into this high
            if (singlelist.size() <= 1) return false;
            for (i = 0; i < singlelist.size() - 1; ++i) {
                Varnode? vn1 = singlelist[i];
                if (vn1 == (Varnode)null) continue;
                for (j = i + 1; j < singlelist.size(); ++j) {
                    Varnode? vn2 = singlelist[j];
                    if (vn2 == (Varnode)null) continue;
                    if (!vn1.copyShadow(vn2)) continue;
                    if (vn2.getCover().containVarnodeDef(vn1) == 1) {
                        data.opSetInput(vn1.getDef(), vn2, 0);
                        res = true;
                        break;
                    }
                    else if (vn1.getCover().containVarnodeDef(vn2) == 1) {
                        data.opSetInput(vn2.getDef(), vn1, 0);
                        singlelist[j] = (Varnode)null;
                        res = true;
                    }
                }
            }
            return res;
        }

        /// \brief Try to reduce/eliminate COPYs produced by the merge trimming process
        ///
        /// In order to force merging of certain Varnodes, extra COPY operations may be inserted
        /// to reduce their Cover ranges, and multiple COPYs from the same Varnode can be created this way.
        /// This method collects sets of COPYs generated in this way that have the same input Varnode
        /// and then tries to replace the COPYs with fewer or a single COPY.
        public void processCopyTrims()
        {
            List<HighVariable> multiCopy = new List<HighVariable>();

            for (int i = 0; i < copyTrims.size(); ++i) {
                HighVariable high = copyTrims[i].getOut().getHigh();
                if (!high.hasCopyIn1()) {
                    multiCopy.Add(high);
                    high.setCopyIn1();
                }
                else
                    high.setCopyIn2();
            }
            copyTrims.Clear();
            for (int i = 0; i < multiCopy.size(); ++i) {
                HighVariable high = multiCopy[i];
                if (high.hasCopyIn2())     // If the high has at least 2 COPYs into it
                    processHighDominantCopy(high);  // Try to replace with a dominant copy
                high.clearCopyIns();
            }
        }

        /// \brief Mark redundant/internal COPY PcodeOps
        ///
        /// Run through all COPY, SUBPIECE, and PIECE operations (PcodeOps that copy data) and
        /// characterize those that are \e internal (copy data between storage locations representing
        /// the same variable) or \e redundant (perform the same copy as an earlier operation).
        /// These, as a result, are not printed in the final source code representation.
        public void markInternalCopies()
        {
            List<HighVariable> multiCopy = new List<HighVariable>();
            PcodeOp op;
            HighVariable h1;
            Varnode v1;
            Varnode v2;
            Varnode v3;
            VariablePiece? p1;
            VariablePiece? p2;
            VariablePiece? p3;
            int val;

            IEnumerator<PcodeOp> iter = data.beginOpAlive();
            while (iter.MoveNext()) {
                op = iter.Current;
                switch (op.code()) {
                    case OpCode.CPUI_COPY:
                        v1 = op.getOut();
                        h1 = v1.getHigh();
                        if (h1 == op.getIn(0).getHigh()) {
                            data.opMarkNonPrinting(op);
                        }
                        else {
                            // COPY between different HighVariables
                            if (!h1.hasCopyIn1()) {
                                // If this is the first COPY we've seen for this high
                                h1.setCopyIn1();       // Mark it
                                multiCopy.Add(h1);
                            }
                            else
                                h1.setCopyIn2();       // This is at least the second COPY we've seen
                            if (v1.hasNoDescend()) {
                                // Don't print shadow assignments
                                if (shadowedVarnode(v1)) {
                                    data.opMarkNonPrinting(op);
                                }
                            }
                        }
                        break;
                    case OpCode.CPUI_PIECE:
                        // Check if output is built out of pieces of itself
                        v1 = op.getOut();
                        v2 = op.getIn(0);
                        v3 = op.getIn(1);
                        p1 = v1.getHigh().piece;
                        p2 = v2.getHigh().piece;
                        p3 = v3.getHigh().piece;
                        if (p1 == (VariablePiece)null) break;
                        if (p2 == (VariablePiece)null) break;
                        if (p3 == (VariablePiece)null) break;
                        if (p1.getGroup() != p2.getGroup()) break;
                        if (p1.getGroup() != p3.getGroup()) break;
                        if (v1.getSpace().isBigEndian()) {
                            if (p2.getOffset() != p1.getOffset()) break;
                            if (p3.getOffset() != p1.getOffset() + v2.getSize()) break;
                        }
                        else {
                            if (p3.getOffset() != p1.getOffset()) break;
                            if (p2.getOffset() != p1.getOffset() + v3.getSize()) break;
                        }
                        data.opMarkNonPrinting(op);
                        break;
                    case OpCode.CPUI_SUBPIECE:
                        v1 = op.getOut();
                        v2 = op.getIn(0);
                        p1 = v1.getHigh().piece;
                        p2 = v2.getHigh().piece;
                        if (p1 == (VariablePiece)null) break;
                        if (p2 == (VariablePiece)null) break;
                        if (p1.getGroup() != p2.getGroup()) break;
                        val = (int)op.getIn(1).getOffset();
                        if (v1.getSpace().isBigEndian()) {
                            if (p2.getOffset() + (v2.getSize() - v1.getSize() - val) != p1.getOffset()) break;
                        }
                        else {
                            if (p2.getOffset() + val != p1.getOffset()) break;
                        }
                        data.opMarkNonPrinting(op);
                        break;
                    default:
                        break;
                }
            }
            for (int i = 0; i < multiCopy.size(); ++i) {
                HighVariable high = multiCopy[i];
                if (high.hasCopyIn2())
                    data.getMerge().processHighRedundantCopy(high);
                high.clearCopyIns();
            }
#if MERGEMULTI_DEBUG
            verifyHighCovers();
#endif
        }

        /// \brief Register an unmapped CONCAT stack with the merge process
        ///
        /// The given Varnode must be the root of a tree of OpCode.CPUI_PIECE operations as produced by
        /// PieceNode::gatherPieces.  These will be grouped together into a single variable.
        /// \param vn is the given root Varnode
        public void registerProtoPartialRoot(Varnode vn)
        {
            protoPartial.Add(vn.getDef());
        }

#if MERGEMULTI_DEBUG
        /// \brief Check that all HighVariable covers are consistent
        ///
        /// For each HighVariable make sure there are no internal intersections between
        /// its instance Varnodes (unless one is a COPY shadow of the other).
        public void verifyHighCovers()
        {
          VarnodeLocSet::const_iterator iter,enditer;

          enditer = data.endLoc();
          for(iter=data.beginLoc();iter!=enditer;++iter) {
            Varnode *vn = *iter;
            if (vn.hasCover()) {
              HighVariable *high = vn.getHigh();
              if (!high.hasCopyIn1()) {
	        high.setCopyIn1();
	        high.verifyCover();
              }
            }
          }
        }
#endif
    }
}
