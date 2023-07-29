using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Sla.DECCORE
{
    /// \brief A class for generating the control-flow structure for a single function
    ///
    /// Control-flow for the function is generated in two phases:  the method generateOps() produces
    /// all the raw p-code ops for the function, and the method generateBlocks() organizes the
    /// p-code ops into basic blocks (PcodeBlockBasic).
    /// In generateOps(), p-code is generated for every machine instruction that is reachable starting
    /// with the entry point address of the function. All possible flow is followed, trimming flow
    /// at instructions that end with the formal RETURN p-code operation.  CALL and CALLIND are treated
    /// as fall-through operations, and flow is not followed into the sub-function.
    ///
    /// The class supports various options for handling corner cases during the flow following process,
    /// including how to handle:
    ///   - Flow out of range (specified by setRange())
    ///   - Flow into unimplemened instructions
    ///   - Flow into unaccessible data
    ///   - Flow into previously traversed data at an \e off cut (\b reinterpreted data)
    ///   - Flow that (seemingly) doesn't end, exceeding a threshold on the number of instructions
    ///
    /// In generateBlocks(), all previously generated PcodeOp instructions are assigned to a
    /// PcodeBlockBasic.  These objects define the formal basic block structure of the function.
    /// Directed control-flow edges between the blocks are created at this time based on the
    /// flow of p-code.
    ///
    /// A Funcdata object provided to the constructor holds:
    ///   - The generated PcodeOp objects (within its PcodeOpBank).
    ///   - The control-flow graph (within its BlockGraph)
    ///   - The list of discovered sub-function calls (as FuncCallSpec objects)
    ///
    /// The Translate object (provided by the Architecture owning the function) generates
    /// the raw p-code ops for a single instruction.  This FlowInfo class also handles
    /// p-code \e injection triggers encountered during flow following, primarily using
    /// the architecture's PcodeInjectLibrary to resolve them.
    internal class FlowInfo
    {
        [Flags()]
        public enum FlowFlag
        {
            /// Ignore/truncate flow into addresses out of the specified range
            ignore_outofbounds = 1,
            /// Treat unimplemented instructions as a NOP (no operation)
            ignore_unimplemented = 2,
            /// Throw an exception for flow into addresses out of the specified range
            error_outofbounds = 4,
            /// Throw an exception for flow into unimplemented instructions
            error_unimplemented = 8,
            /// Throw an exception for flow into previously encountered data at a difference \e cut
            error_reinterpreted = 0x10,
            /// Throw an exception if too many instructions are encountered
            error_toomanyinstructions = 0x20,
            /// Indicate we have encountered unimplemented instructions
            unimplemented_present = 0x40,
            /// Indicate we have encountered flow into unaccessible data
            baddata_present = 0x80,
            /// Indicate we have encountered flow out of the specified range
            outofbounds_present = 0x100,
            /// Indicate we have encountered reinterpreted data
            reinterpreted_present = 0x200,
            /// Indicate the maximum instruction threshold was reached
            toomanyinstructions_present = 0x400,
            /// Indicate a CALL was converted to a BRANCH and some code may be unreachable
            possible_unreachable = 0x1000,
            /// Indicate flow is being generated to in-line (a function)
            flow_forinline = 0x2000,
            /// Indicate that any jump table recovery should record the table structure
            record_jumploads = 0x4000
        }

        /// \brief A helper function describing the number of bytes in a machine instruction and the starting p-code op
        private struct VisitStat
        {
            /// Sequence number of first PcodeOp in the instruction (or INVALID if no p-code)
            internal SeqNum seqnum;
            /// Number of bytes in the instruction
            internal int size;
        }

        /// Owner of the function
        private Architecture glb;
        /// The function being flow-followed
        private Funcdata data;
        /// Container for generated p-code
        private PcodeOpBank obank;
        /// Container for the control-flow graph
        private BlockGraph bblocks;
        /// The list of discovered sub-function call sites
        private List<FuncCallSpecs> qlst;
        /// PCodeOp factory (configured to allocate into \b data and \b obank)
        private PcodeEmitFd emitter;
        /// Addresses which are permanently unprocessed
        private List<Address> unprocessed;
        /// Addresses to which there is flow
        private List<Address> addrlist;
        /// List of BRANCHIND ops (preparing for jump table recovery)
        private List<PcodeOp> tablelist;
        /// List of p-code ops that need injection
        private List<PcodeOp> injectlist;
        /// Map of machine instructions that have been visited so far
        private Dictionary<Address, VisitStat> visited;
        /// Source p-code op (Edges between basic blocks)
        private List<PcodeOp> block_edge1;
        /// Destination p-code op (Edges between basic blocks)
        private List<PcodeOp> block_edge2;
        /// Number of instructions flowed through
        private uint insn_count;
        /// Maximum number of instructions
        private uint insn_max;
        /// Start of range in which we are allowed to flow
        private Address baddr;
        /// End of range in which we are allowed to flow
        private Address eaddr;
        /// Start of actual function range
        private Address minaddr;
        /// End of actual function range
        private Address maxaddr;
        /// Does the function have registered flow override instructions
        private bool flowoverride_present;
        /// Boolean options for flow following
        private uint flags;
        /// First function in the in-lining chain
        private Funcdata inline_head;
        /// Active list of addresses for function that are in-lined
        private HashSet<Address> inline_recursion;
        /// Storage for addresses of functions that are in-lined
        private HashSet<Address> inline_base;

        /// Are there possible unreachable ops
        private bool hasPossibleUnreachable() => ((flags & possible_unreachable)!=0);

        /// Mark that there may be unreachable ops
        private void setPossibleUnreachable()
        {
            flags |= possible_unreachable;
        }

        /// Clear any discovered flow properties
        private void clearProperties()
        {
            flags &= ~((uint)(unimplemented_present | baddata_present | outofbounds_present));
            insn_count = 0;
        }

        /// Has the given instruction (address) been seen in flow
        private bool seenInstruction(Address addr)
        {
            return (visited.find(addr) != visited.end());
        }

        /// Find fallthru pcode-op for given op
        /// For efficiency, this method assumes the given op can actually fall-thru.
        /// \param op is the given PcodeOp
        /// \return the PcodeOp that fall-thru flow would reach (or NULL if there is no possible p-code op)
        private PcodeOp fallthruOp(PcodeOp op)
        {
            PcodeOp* retop;
            list<PcodeOp*>::const_iterator iter = op.getInsertIter();
            ++iter;
            if (iter != obank.endDead())
            {
                retop = *iter;
                if (!retop.isInstructionStart()) // If within same instruction
                    return retop;       // Then this is the fall thru
            }
            // Find address of instruction containing this op
            map<Address, VisitStat>::const_iterator miter;
            miter = visited.upper_bound(op.getAddr());
            if (miter == visited.begin()) return (PcodeOp)null;
            --miter;
            if ((*miter).first + (*miter).second.size <= op.getAddr())
                return (PcodeOp)null;
            return target((*miter).first + (*miter).second.size);
        }

        /// Register a new (non fall-thru) flow target
        /// Check to see if the new target has been seen before. Otherwise
        /// add it to the list of addresses that need to be processed.
        /// Also check range bounds and update basic block information.
        /// \param from is the PcodeOp issuing the branch
        /// \param to is the target address of the branch
        private void newAddress(PcodeOp from, Address to)
        {
            if ((to < baddr) || (eaddr < to))
            {
                handleOutOfBounds(from.getAddr(), to);
                unprocessed.Add(to);
                return;
            }

            if (seenInstruction(to))
            {   // If we have seen this address before
                PcodeOp* op = target(to);
                data.opMarkStartBasic(op);
                return;
            }
            addrlist.Add(to);
        }

        /// \brief Delete any remaining ops at the end of the instruction
        ///
        /// (because they have been predetermined to be dead)
        /// \param oiter is the point within the raw p-code list where deletion should start
        private void deleteRemainingOps(IEnumerator<PcodeOp> oiter)
        {
            while (oiter != obank.endDead())
            {
                PcodeOp* op = *oiter;
                ++oiter;
                data.opDestroyRaw(op);
            }
        }

        /// \brief Analyze control-flow within p-code for a single instruction
        ///
        /// Walk through the raw p-code (from the given iterator to the end of the list)
        /// looking for control flow operations (BRANCH,CBRANCH,BRANCHIND,CALL,CALLIND,RETURN)
        /// and add appropriate annotations (startbasic, callspecs, new addresses).
        /// As it iterates through the p-code, the method maintains a reference to a boolean
        /// indicating whether the current op is the start of a basic block. This value
        /// persists across calls. The method also passes back a boolean value indicating whether
        /// the instruction as a whole has fall-thru flow.
        /// \param oiter is the given iterator starting the list of p-code ops
        /// \param startbasic is the reference holding whether the current op starts a basic block
        /// \param isfallthru passes back if the instruction has fall-thru flow
        /// \param fc if the p-code is generated from an \e injection, this holds the reference to the injecting sub-function
        /// \return the last processed PcodeOp (or NULL if there were no ops in the instruction)
        private PcodeOp xrefControlFlow(IEnumerator<PcodeOp> oiter, bool startbasic,
            bool isfallthru, FuncCallSpecs fc)
        {
            PcodeOp* op = (PcodeOp)null;
            isfallthru = false;
            uint maxtime = 0;  // Deepest internal relative branch
            while (oiter != obank.endDead())
            {
                op = *oiter++;
                if (startbasic)
                {
                    data.opMarkStartBasic(op);
                    startbasic = false;
                }
                switch (op.code())
                {
                    case CPUI_CBRANCH:
                        {
                            Address destaddr = op.getIn(0).getAddr();
                            if (destaddr.isConstant())
                            {
                                Address fallThruAddr;
                                PcodeOp* destop = findRelTarget(op, fallThruAddr);
                                if (destop != (PcodeOp)null)
                                {
                                    data.opMarkStartBasic(destop);  // Make sure the target op is a basic block start
                                    uint newtime = destop.getTime();
                                    if (newtime > maxtime)
                                        maxtime = newtime;
                                }
                                else
                                    isfallthru = true;      // Relative branch is to end of instruction
                            }
                            else
                                newAddress(op, destaddr); // Generate branch address
                            startbasic = true;
                        }
                        break;
                    case CPUI_BRANCH:
                        {
                            Address destaddr = op.getIn(0).getAddr();
                            if (destaddr.isConstant())
                            {
                                Address fallThruAddr;
                                PcodeOp* destop = findRelTarget(op, fallThruAddr);
                                if (destop != (PcodeOp)null)
                                {
                                    data.opMarkStartBasic(destop);  // Make sure the target op is a basic block start
                                    uint newtime = destop.getTime();
                                    if (newtime > maxtime)
                                        maxtime = newtime;
                                }
                                else
                                    isfallthru = true;      // Relative branch is to end of instruction
                            }
                            else
                                newAddress(op, destaddr); // Generate branch address
                            if (op.getTime() >= maxtime)
                            {
                                deleteRemainingOps(oiter);
                                oiter = obank.endDead();
                            }
                            startbasic = true;
                        }
                        break;
                    case CPUI_BRANCHIND:
                        tablelist.Add(op);    // Put off trying to recover the table
                        if (op.getTime() >= maxtime)
                        {
                            deleteRemainingOps(oiter);
                            oiter = obank.endDead();
                        }
                        startbasic = true;
                        break;
                    case CPUI_RETURN:
                        if (op.getTime() >= maxtime)
                        {
                            deleteRemainingOps(oiter);
                            oiter = obank.endDead();
                        }
                        startbasic = true;
                        break;
                    case CPUI_CALL:
                        if (setupCallSpecs(op, fc))
                            --oiter;        // Backup one op, to pickup halt
                        break;
                    case CPUI_CALLIND:
                        if (setupCallindSpecs(op, fc))
                            --oiter;        // Backup one op, to pickup halt
                        break;
                    case CPUI_CALLOTHER:
                        {
                            InjectedUserOp* userop = dynamic_cast<InjectedUserOp*>(glb.userops.getOp(op.getIn(0).getOffset()));
                            if (userop != (InjectedUserOp*)0)
                                injectlist.Add(op);
                            break;
                        }
                    default:
                        break;
                }
            }
            if (isfallthru)     // We have seen an explicit relative branch to end of instruction
                startbasic = true;      // So we know next instruction starts a basicblock
            else
            {           // If we haven't seen a relative branch, calculate fallthru by looking at last op
                if (op == (PcodeOp)null)
                    isfallthru = true;  // No ops at all, mean a fallthru
                else
                {
                    switch (op.code())
                    {
                        case CPUI_BRANCH:
                        case CPUI_BRANCHIND:
                        case CPUI_RETURN:
                            break;          // If the last instruction is a branch, then no fallthru
                        default:
                            isfallthru = true;  // otherwise it is a fallthru
                            break;
                    }
                }
            }
            return op;
        }

        /// \brief Generate p-code for a single machine instruction and process discovered flow information
        ///
        /// P-code is generated (to the raw \e dead list in PcodeOpBank). Errors for unimplemented
        /// instructions or inaccessible data are handled.  The p-code is examined for control-flow,
        /// and annotations are made.  The set of visited instructions and the set of
        /// addresses still needing to be processed are updated.
        /// \param curaddr is the address of the instruction to process
        /// \param startbasic indicates of the instruction starts a basic block and passes back whether the next instruction does
        /// \return \b true if the processed instruction has a fall-thru flow
        private bool processInstruction(Address curaddr, bool startbasic)
        {
            bool emptyflag;
            bool isfallthru = true;
            //  JumpTable *jt;
            list<PcodeOp*>::const_iterator oiter;
            int step;
            uint flowoverride;

            if (insn_count >= insn_max)
            {
                if ((flags & error_toomanyinstructions) != 0)
                    throw new LowlevelError("Flow exceeded maximum allowable instructions");
                else
                {
                    step = 1;
                    artificialHalt(curaddr, PcodeOp::badinstruction);
                    data.warning("Too many instructions -- Truncating flow here", curaddr);
                    if (!hasTooManyInstructions())
                    {
                        flags |= toomanyinstructions_present;
                        data.warningHeader("Exceeded maximum allowable instructions: Some flow is truncated");
                    }
                }
            }
            insn_count += 1;

            if (obank.empty())
                emptyflag = true;
            else
            {
                emptyflag = false;
                oiter = obank.endDead();
                --oiter;
            }
            if (flowoverride_present)
                flowoverride = data.getOverride().getFlowOverride(curaddr);
            else
                flowoverride = Override::NONE;

            try
            {
                step = glb.translate.oneInstruction(emitter, curaddr); // Generate ops for instruction
            }
            catch (UnimplError rr) {  // Instruction is unimplemented
                if ((flags & ignore_unimplemented) != 0)
                {
                    step = err.instruction_length;
                    if (!hasUnimplemented())
                    {
                        flags |= unimplemented_present;
                        data.warningHeader("Control flow ignored unimplemented instructions");
                    }
                }
                else if ((flags & error_unimplemented) != 0)
                    throw err;      // rethrow
                else
                {
                    // Add infinite loop instruction
                    step = 1;           // Pretend size 1
                    artificialHalt(curaddr, PcodeOp::unimplemented);
                    data.warning("Unimplemented instruction - Truncating control flow here", curaddr);
                    if (!hasUnimplemented())
                    {
                        flags |= unimplemented_present;
                        data.warningHeader("Control flow encountered unimplemented instructions");
                    }
                }
            }
  catch (BadDataError err) {
                if ((flags & error_unimplemented) != 0)
                    throw err;      // rethrow
                else
                {
                    // Add infinite loop instruction
                    step = 1;           // Pretend size 1
                    artificialHalt(curaddr, PcodeOp::badinstruction);
                    data.warning("Bad instruction - Truncating control flow here", curaddr);
                    if (!hasBadData())
                    {
                        flags |= baddata_present;
                        data.warningHeader("Control flow encountered bad instruction data");
                    }
                }
            }
            VisitStat & stat(visited[curaddr]); // Mark that we visited this instruction
            stat.size = step;       // Record size of instruction

            if (curaddr < minaddr)  // Update minimum and maximum address
                minaddr = curaddr;
            if (maxaddr < curaddr + step)   // Keep track of biggest and smallest address
                maxaddr = curaddr + step;

            if (emptyflag)      // Make sure oiter points at first new op
                oiter = obank.beginDead();
            else
                ++oiter;

            if (oiter != obank.endDead())
            {
                stat.seqnum = (*oiter).getSeqNum();
                data.opMarkStartInstruction(*oiter); // Mark the first op in the instruction
                if (flowoverride != Override::NONE)
                    data.overrideFlow(curaddr, flowoverride);
                xrefControlFlow(oiter, startbasic, isfallthru, (FuncCallSpecs*)0);
            }

            if (isfallthru)
                addrlist.Add(curaddr + step);
            return isfallthru;
        }

        /// Process (the next) sequence of instructions in fall-thru order
        /// The address at the top stack that still needs processing is popped.
        /// P-code is generated for instructions starting at this address until
        /// one no longer has fall-thru flow (or some other error occurs).
        private void fallthru()
        {
            Address bound;

            if (!setFallthruBound(bound)) return;

            Address curaddr;
            bool startbasic = true;
            bool fallthruflag;

            for (; ; )
            {
                curaddr = addrlist.back();
                addrlist.pop_back();
                fallthruflag = processInstruction(curaddr, startbasic);
                if (!fallthruflag) break;
                if (addrlist.empty()) break;
                if (bound <= addrlist.back())
                {
                    if (bound == eaddr)
                    {
                        handleOutOfBounds(eaddr, addrlist.back());
                        unprocessed.Add(addrlist.back());
                        addrlist.pop_back();
                        return;
                    }
                    if (bound == addrlist.back())
                    { // Hit the bound exactly
                        if (startbasic)
                        {
                            PcodeOp* op = target(addrlist.back());
                            data.opMarkStartBasic(op);
                        }
                        addrlist.pop_back();
                        break;
                    }
                    if (!setFallthruBound(bound)) return; // Reset bound
                }
            }
        }

        /// \brief Generate the target PcodeOp for a relative branch
        ///
        /// Assuming the given op is a relative branch, find the existing target PcodeOp if the
        /// branch is properly internal, or return the fall-thru address in \b res (which may not have
        /// PcodeOps generated for it yet) if the relative branch is really a branch to the next instruction.
        /// Otherwise an exception is thrown.
        /// \param op is the given branching p-code op
        /// \param res is a reference to the fall-thru address being passed back
        /// \return the target PcodeOp or NULL if the fall-thru address is passed back instead
        private PcodeOp findRelTarget(PcodeOp op, Address res)
        {
            Address addr = op.getIn(0).getAddr();
            uint id = op.getTime() + addr.getOffset();
            SeqNum seqnum(op.getAddr(), id);
            PcodeOp* retop = obank.findOp(seqnum);
            if (retop != (PcodeOp)null)   // Is this a "properly" internal branch
                return retop;

            // Now we check if the relative branch is really to the next instruction
            SeqNum seqnum1 = new SeqNum(op.getAddr(), id-1);
            retop = obank.findOp(seqnum1); // We go back one sequence number
            if (retop != (PcodeOp)null)
            {
                // If the PcodeOp exists here then branch was indeed to next instruction
                map<Address, VisitStat>::const_iterator miter;
                miter = visited.upper_bound(retop.getAddr());
                if (miter != visited.begin())
                {
                    --miter;
                    res = (*miter).first + (*miter).second.size;
                    if (op.getAddr() < res)
                        return (PcodeOp)null; // Indicate that res has the fallthru address
                }
            }
            ostringstream errmsg;
            errmsg << "Bad relative branch at instruction : (";
            errmsg << op.getAddr().getSpace().getName() << ',';
            op.getAddr().printRaw(errmsg);
            errmsg << ')';
            throw new LowlevelError(errmsg.str());
        }

        /// Add any remaining un-followed addresses to the \b unprocessed list
        /// In the case where additional flow is truncated, run through the list of
        /// pending addresses, and if they don't have a p-code generated for them,
        /// add the Address to the \b unprocessed array.
        private void findUnprocessed()
        {
            List<Address>::iterator iter;

            for (iter = addrlist.begin(); iter != addrlist.end(); ++iter)
            {
                if (seenInstruction(*iter))
                {
                    PcodeOp* op = target(*iter);
                    data.opMarkStartBasic(op);
                }
                else
                    unprocessed.Add(*iter);
            }
        }

        /// Get rid of duplicates in the \b unprocessed list
        /// The list is also sorted
        private void dedupUnprocessed()
        {
            if (unprocessed.empty()) return;
            sort(unprocessed.begin(), unprocessed.end());
            List<Address>::iterator iter1, iter2;

            iter1 = unprocessed.begin();
            Address lastaddr = *iter1++;
            iter2 = iter1;
            while (iter1 != unprocessed.end())
            {
                if (*iter1 == lastaddr)
                    iter1++;
                else
                {
                    lastaddr = *iter1++;
                    *iter2++ = lastaddr;
                }
            }
            unprocessed.erase(iter2, unprocessed.end());
        }

        /// Fill-in artificial HALT p-code for \b unprocessed addresses
        /// A special form of RETURN instruction is generated for every address in
        /// the \b unprocessed list.
        private void fillinBranchStubs()
        {
            List<Address>::iterator iter;

            findUnprocessed();
            dedupUnprocessed();
            for (iter = unprocessed.begin(); iter != unprocessed.end(); ++iter)
            {
                PcodeOp* op = artificialHalt(*iter, PcodeOp::missing);
                data.opMarkStartBasic(op);
                data.opMarkStartInstruction(op);
            }
        }

        /// Collect edges between basic blocks as PcodeOp to PcodeOp pairs
        /// An edge is held as matching PcodeOp entries in \b block_edge1 and \b block_edge2.
        /// Edges are generated for fall-thru to a p-code op marked as the start of a basic block
        /// or for an explicit branch.
        private void collectEdges()
        {
            list<PcodeOp*>::const_iterator iter, iterend, iter1, iter2;
            PcodeOp* op,*targ_op;
            JumpTable* jt;
            bool nextstart;
            int i, num;

            if (bblocks.getSize() != 0)
                throw RecovError("Basic blocks already calculated\n");

            iter = obank.beginDead();
            iterend = obank.endDead();
            while (iter != iterend)
            {
                op = *iter++;
                if (iter == iterend)
                    nextstart = true;
                else
                    nextstart = (*iter).isBlockStart();
                switch (op.code())
                {
                    case CPUI_BRANCH:
                        targ_op = branchTarget(op);
                        block_edge1.Add(op);
                        //      block_edge2.Add(op.Input(0).getAddr().Iop());
                        block_edge2.Add(targ_op);
                        break;
                    case CPUI_BRANCHIND:
                        jt = data.findJumpTable(op);
                        if (jt == (JumpTable*)0) break;
                        // If we are in this routine and there is no table
                        // Then we must be doing partial flow analysis
                        // so assume there are no branches out
                        num = jt.numEntries();
                        for (i = 0; i < num; ++i)
                        {
                            targ_op = target(jt.getAddressByIndex(i));
                            if (targ_op.isMark()) continue; // Already a link between these blocks
                            targ_op.setMark();
                            block_edge1.Add(op);
                            block_edge2.Add(targ_op);
                        }
                        iter1 = block_edge1.end(); // Clean up our marks
                        iter2 = block_edge2.end();
                        while (iter1 != block_edge1.begin())
                        {
                            --iter1;
                            --iter2;
                            if ((*iter1) == op)
                                (*iter2).clearMark();
                            else
                                break;
                        }
                        break;
                    case CPUI_RETURN:
                        break;
                    case CPUI_CBRANCH:
                        targ_op = fallthruOp(op); // Put in fallthru edge
                        block_edge1.Add(op);
                        block_edge2.Add(targ_op);
                        targ_op = branchTarget(op);
                        block_edge1.Add(op);
                        block_edge2.Add(targ_op);
                        break;
                    default:
                        if (nextstart)
                        {       // Put in fallthru edge if new basic block
                            targ_op = fallthruOp(op);
                            block_edge1.Add(op);
                            block_edge2.Add(targ_op);
                        }
                        break;
                }
            }
        }

        /// Split raw p-code ops up into basic blocks
        /// PcodeOp objects are moved out of the PcodeOpBank \e dead list into their
        /// assigned PcodeBlockBasic.  Initial address ranges of instructions are recorded in the block.
        /// PcodeBlockBasic objects are created based on p-code ops that have been
        /// previously marked as \e start of basic block.
        private void splitBasic()
        {
            PcodeOp* op;
            BlockBasic* cur;
            list<PcodeOp*>::const_iterator iter, iterend;

            iter = obank.beginDead();
            iterend = obank.endDead();
            if (iter == iterend) return;
            op = *iter++;
            if (!op.isBlockStart())
                throw new LowlevelError("First op not marked as entry point");
            cur = bblocks.newBlockBasic(&data);
            data.opInsert(op, cur, cur.endOp());
            bblocks.setStartBlock(cur);
            Address start = op.getAddr();
            Address stop = start;
            while (iter != iterend)
            {
                op = *iter++;
                if (op.isBlockStart())
                {
                    data.setBasicBlockRange(cur, start, stop);
                    cur = bblocks.newBlockBasic(&data); // Set up the next basic block
                    start = op.getSeqNum().getAddr();
                    stop = start;
                }
                else
                {
                    Address nextAddr = op.getAddr();
                    if (stop < nextAddr)
                        stop = nextAddr;
                }
                data.opInsert(op, cur, cur.endOp());
            }
            data.setBasicBlockRange(cur, start, stop);
        }

        /// Generate edges between basic blocks
        /// Directed edges between the PcodeBlockBasic objects are created based on the
        /// previously collected p-code op pairs in \b block_edge1 and \b block_edge2
        private void connectBasic()
        {
            PcodeOp* op,*targ_op;
            BlockBasic* bs,*targ_bs;
            list<PcodeOp*>::const_iterator iter, iter2;

            iter = block_edge1.begin();
            iter2 = block_edge2.begin();
            while (iter != block_edge1.end())
            {
                op = *iter++;
                targ_op = *iter2++;
                bs = op.getParent();
                targ_bs = targ_op.getParent();
                bblocks.addEdge(bs, targ_bs);
            }
        }

        /// Find end of the next unprocessed region
        /// From the address at the top of the \b addrlist stack
        /// Figure out how far we could follow fall-thru instructions
        /// before hitting something we've already seen
        /// \param bound passes back the first address encountered that we have already seen
        /// \return \b false if the address has already been visited
        private bool setFallthruBound(Address bound)
        {
            map<Address, VisitStat>::const_iterator iter;
            Address addr = addrlist.back();

            iter = visited.upper_bound(addr); // First range greater than addr
            if (iter != visited.begin())
            {
                --iter;         // Last range less than or equal to us
                if (addr == (*iter).first)
                { // If we have already visited this address
                    PcodeOp* op = target(addr); // But make sure the address
                    data.opMarkStartBasic(op); // starts a basic block
                    addrlist.pop_back();    // Throw it away
                    return false;
                }
                if (addr < (*iter).first + (*iter).second.size)
                    reinterpreted(addr);
                ++iter;
            }
            if (iter != visited.end())  // Whats the maximum distance we can go
                bound = (*iter).first;
            else
                bound = eaddr;
            return true;
        }

        /// \brief Generate warning message or throw exception for given flow that is out of bounds
        ///
        /// \param fromaddr is the source address of the flow (presumably in bounds)
        /// \param toaddr is the given destination address that is out of bounds
        private void handleOutOfBounds(Address fromaddr, Address toaddr)
        {
            if ((flags & ignore_outofbounds) == 0)
            { // Should we throw an error for out of bounds
                ostringstream errmsg;
                errmsg << "Function flow out of bounds: ";
                errmsg << fromaddr.getShortcut();
                fromaddr.printRaw(errmsg);
                errmsg << " flows to ";
                errmsg << toaddr.getShortcut();
                toaddr.printRaw(errmsg);
                if ((flags & error_outofbounds) == 0)
                {
                    data.warning(errmsg.str(), toaddr);
                    if (!hasOutOfBounds())
                    {
                        flags |= outofbounds_present;
                        data.warningHeader("Function flows out of bounds");
                    }
                }
                else
                    throw new LowlevelError(errmsg.str());
            }
        }

        /// Create an artificial halt p-code op
        /// An \b artificial \b halt, is a special form of RETURN op.
        /// The op is annotated with the desired \e type of artificial halt.
        ///   - Bad instruction
        ///   - Unimplemented instruction
        ///   - Missing/truncated instruction
        ///   - (Previous) call that never returns
        ///
        /// \param addr is the target address for the new p-code op
        /// \param flag is the desired \e type
        /// \return the new p-code op
        private PcodeOp artificialHalt(Address addr, uint flag)
        {
            PcodeOp* haltop = data.newOp(1, addr);
            data.opSetOpcode(haltop, CPUI_RETURN);
            data.opSetInput(haltop, data.newConstant(4, 1), 0);
            if (flag != 0)
                data.opMarkHalt(haltop, flag); // What kind of halt
            return haltop;
        }

        /// Generate warning message or exception for a \e reinterpreted address
        /// A set of bytes is \b reinterpreted if there are at least two
        /// different interpretations of the bytes as instructions.
        /// \param addr is the address of a byte previously interpreted as (the interior of) an instruction
        private void reinterpreted(Address addr)
        {
            map<Address, VisitStat>::const_iterator iter;

            iter = visited.upper_bound(addr);
            if (iter == visited.begin()) return; // Should never happen
            --iter;
            Address addr2 = (*iter).first;
            ostringstream s;

            s << "Instruction at (" << addr.getSpace().getName() << ',';
            addr.printRaw(s);
            s << ") overlaps instruction at (" << addr2.getSpace().getName() << ',';
            addr2.printRaw(s);
            s << ')' << endl;
            if ((flags & error_reinterpreted) != 0)
                throw new LowlevelError(s.str());

            if ((flags & reinterpreted_present) == 0)
            {
                flags |= reinterpreted_present;
                data.warningHeader(s.str());
            }
        }

        /// \brief Check for modifications to flow at a call site given the recovered FuncCallSpecs
        ///
        /// The sub-function may be in-lined or never return.
        /// \param fspecs is the given call site
        /// \return \b true if the sub-function never returns
        private bool checkForFlowModification(FuncCallSpecs fspecs)
        {
            if (fspecs.isInline())
                injectlist.Add(fspecs.getOp());
            if (fspecs.isNoReturn())
            {
                PcodeOp* op = fspecs.getOp();
                PcodeOp* haltop = artificialHalt(op.getAddr(), PcodeOp::noreturn);
                data.opDeadInsertAfter(haltop, op);
                if (!fspecs.isInline())
                    data.warning("Subroutine does not return", op.getAddr());
                return true;
            }

            return false;
        }

        /// Try to recover the Funcdata object corresponding to a given call
        /// If there is an explicit target address for the given call site,
        /// attempt to look up the function and adjust information in the FuncCallSpecs call site object.
        /// \param fspecs is the call site object
        private void queryCall(FuncCallSpecs fspecs)
        {
            if (!fspecs.getEntryAddress().isInvalid())
            { // If this is a direct call
                Funcdata* otherfunc = data.getScopeLocal().getParent().queryFunction(fspecs.getEntryAddress());
                if (otherfunc != (Funcdata)null)
                {
                    fspecs.setFuncdata(otherfunc); // Associate the symbol with the callsite
                    if (!fspecs.hasModel() || otherfunc.getFuncProto().isInline())
                    {   // If the prototype was not overridden
                        fspecs.copyFlowEffects(otherfunc.getFuncProto());  // Take the flow affects of the symbol
                                                                            // If the call site is applying just the standard prototype from the symbol,
                                                                            // this postpones the full copy of the prototype until ActionDefaultParams
                                                                            // Which lets "last second" changes come in, between when the function is first walked and
                                                                            // when it is finally decompiled
                    }
                }
            }
        }

        /// Set up the FuncCallSpecs object for a new call site
        /// The new FuncCallSpecs object is created and initialized based on
        /// the CALL op at the site and any matching function in the symbol table.
        /// Any overriding prototype or control-flow is examined and applied.
        /// \param op is the given CALL op
        /// \param fc is non-NULL if \e injection is in progress and a cycle check needs to be made
        /// \return \b true if it is discovered the sub-function never returns
        private bool setupCallSpecs(PcodeOp op, FuncCallSpecs fc)
        {
            FuncCallSpecs* res;
            res = new FuncCallSpecs(op);
            data.opSetInput(op, data.newVarnodeCallSpecs(res), 0);
            qlst.Add(res);

            data.getOverride().applyPrototype(data, *res);
            queryCall(*res);
            if (fc != (FuncCallSpecs*)0)
            {   // If we are already in the midst of an injection
                if (fc.getEntryAddress() == res.getEntryAddress())
                    res.cancelInjectId();      // Don't allow recursion
            }
            return checkForFlowModification(*res);
        }

        /// \brief Set up the FuncCallSpecs object for a new indirect call site
        ///
        /// The new FuncCallSpecs object is created and initialized based on
        /// the CALLIND op at the site. Any overriding prototype or control-flow may be examined and applied.
        /// \param op is the given CALLIND op
        /// \param fc is non-NULL if \e injection is in progress and a cycle check needs to be made
        /// \return \b true if it is discovered the sub-function never returns
        private bool setupCallindSpecs(PcodeOp op, FuncCallSpecs fc)
        {
            FuncCallSpecs* res;
            res = new FuncCallSpecs(op);
            qlst.Add(res);

            data.getOverride().applyIndirect(data, *res);
            if (fc != (FuncCallSpecs*)0 && fc.getEntryAddress() == res.getEntryAddress())
                res.setAddress(Address()); // Cancel any indirect override
            data.getOverride().applyPrototype(data, *res);
            queryCall(*res);

            if (!res.getEntryAddress().isInvalid())
            {   // If we are overridden to a direct call
                // Change indirect pcode call into a normal pcode call
                data.opSetOpcode(op, CPUI_CALL); // Set normal opcode
                data.opSetInput(op, data.newVarnodeCallSpecs(res), 0);
            }
            return checkForFlowModification(*res);
        }

        /// Check for control-flow in a new injected p-code op
        /// If the given injected op is a CALL, CALLIND, or BRANCHIND,
        /// we need to add references to it in other flow tables.
        /// \param op is the given injected p-code op
        private void xrefInlinedBranch(PcodeOp op)
        {
            if (op.code() == CPUI_CALL)
                setupCallSpecs(op, (FuncCallSpecs*)0);
            else if (op.code() == CPUI_CALLIND)
                setupCallindSpecs(op, (FuncCallSpecs*)0);
            else if (op.code() == CPUI_BRANCHIND)
            {
                JumpTable* jt = data.linkJumpTable(op);
                if (jt == (JumpTable*)0)
                    tablelist.Add(op); // Didn't recover a jumptable
            }
        }

        /// \brief Inject the given payload into \b this flow
        ///
        /// The injected p-code replaces the given op, and control-flow information
        /// is updated.
        /// \param payload is the specific \e injection payload
        /// \param icontext is the specific context for the injection
        /// \param op is the given p-code op being replaced by the payload
        /// \param fc (if non-NULL) is information about the call site being in-lined
        private void doInjection(InjectPayload payload, InjectContext icontext, PcodeOp op,
            FuncCallSpecs fc)
        {
            // Create marker at current end of the deadlist
            list<PcodeOp*>::const_iterator iter = obank.endDead();
            --iter;         // There must be at least one op

            payload.inject(icontext, emitter);     // Do the injection

            bool startbasic = op.isBlockStart();
            ++iter;         // Now points to first op in the injection
            if (iter == obank.endDead())
                throw new LowlevelError("Empty injection: " + payload.getName());
            PcodeOp* firstop = *iter;
            bool isfallthru = true;
            PcodeOp* lastop = xrefControlFlow(iter, startbasic, isfallthru, fc);

            if (startbasic)
            {       // If the inject code does NOT fall thru
                iter = op.getInsertIter();
                ++iter;         // Mark next op after the call
                if (iter != obank.endDead())
                    data.opMarkStartBasic(*iter); // as start of basic block
            }

            if (payload.isIncidentalCopy())
                obank.markIncidentalCopy(firstop, lastop);
            obank.moveSequenceDead(firstop, lastop, op); // Move the injection to right after the call

            map<Address, VisitStat>::iterator viter = visited.find(op.getAddr());
            if (viter != visited.end())
            {               // Check if -op- is a possible branch target
                if ((*viter).second.seqnum == op.getSeqNum())  // (if injection op is the first op for its address)
                    (*viter).second.seqnum = firstop.getSeqNum();  //    change the seqnum to the first injected op
            }
            // Get rid of the original call
            data.opDestroyRaw(op);
        }

        /// Perform \e injection for a given user-defined p-code op
        /// The op must already be established as a user defined op with an associated injection
        /// \param op is the given PcodeOp
        private void injectUserOp(PcodeOp op)
        {
            InjectedUserOp* userop = (InjectedUserOp*)glb.userops.getOp((int)op.getIn(0).getOffset());
            InjectPayload* payload = glb.pcodeinjectlib.getPayload(userop.getInjectId());
            InjectContext & icontext(glb.pcodeinjectlib.getCachedContext());
            icontext.clear();
            icontext.baseaddr = op.getAddr();
            icontext.nextaddr = icontext.baseaddr;
            for (int i = 1; i < op.numInput(); ++i)
            {       // Skip the first operand containing the injectid
                Varnode* vn = op.getIn(i);
                icontext.inputlist.emplace_back();
                icontext.inputlist.back().space = vn.getSpace();
                icontext.inputlist.back().offset = vn.getOffset();
                icontext.inputlist.back().size = vn.getSize();
            }
            Varnode* outvn = op.getOut();
            if (outvn != (Varnode)null)
            {
                icontext.output.emplace_back();
                icontext.output.back().space = outvn.getSpace();
                icontext.output.back().offset = outvn.getOffset();
                icontext.output.back().size = outvn.getSize();
            }
            doInjection(payload, icontext, op, (FuncCallSpecs*)0);
        }

        /// In-line the sub-function at the given call site
        /// P-code is generated for the sub-function and then woven into \b this flow
        /// at the call site.
        /// \param fc is the given call site
        /// \return \b true if the in-lining is successful
        private bool inlineSubFunction(FuncCallSpecs fc)
        {
            Funcdata* fd = fc.getFuncdata();
            if (fd == (Funcdata)null) return false;
            PcodeOp* op = fc.getOp();
            Address retaddr;

            if (!data.inlineFlow(fd, *this, op))
                return false;

            // Changing CALL to JUMP may make some original code unreachable
            setPossibleUnreachable();

            return true;
        }

        /// Perform \e injection replacing the CALL at the given call site
        /// The call site must be previously marked with the \e injection id.
        /// The PcodeInjectLibrary is queried for the associated payload, which is
        /// then inserted into \b this flow, replacing the original CALL op.
        /// \param fc is the given call site
        /// \return \b true if the injection was successfully performed
        private bool injectSubFunction(FuncCallSpecs fc)
        {
            PcodeOp* op = fc.getOp();

            // Inject to end of the deadlist
            InjectContext & icontext(glb.pcodeinjectlib.getCachedContext());
            icontext.clear();
            icontext.baseaddr = op.getAddr();
            icontext.nextaddr = icontext.baseaddr;
            icontext.calladdr = fc.getEntryAddress();
            InjectPayload* payload = glb.pcodeinjectlib.getPayload(fc.getInjectId());
            doInjection(payload, icontext, op, fc);
            // If the injection fills in the -paramshift- field of the context
            // pass this information on to the callspec of the injected call, which must be last in the list
            if (payload.getParamShift() != 0)
                qlst.back().setParamshift(payload.getParamShift());

            return true;            // Return true to indicate injection happened and callspec should be deleted
        }

        /// \brief Check if any of the calls this function makes are to already traced data-flow.
        ///
        /// If so, we change the CALL to a BRANCH and issue a warning.
        /// This situation is most likely due to a Position Indepent Code construction.
        private void checkContainedCall()
        {
            List<FuncCallSpecs*>::iterator iter;
            for (iter = qlst.begin(); iter != qlst.end(); ++iter)
            {
                FuncCallSpecs* fc = *iter;
                Funcdata* fd = fc.getFuncdata();
                if (fd != (Funcdata)null) continue;
                PcodeOp* op = fc.getOp();
                if (op.code() != CPUI_CALL) continue;

                Address addr = fc.getEntryAddress();
                map<Address, VisitStat>::const_iterator miter;
                miter = visited.upper_bound(addr);
                if (miter == visited.begin()) continue;
                --miter;
                if ((*miter).first + (*miter).second.size <= addr)
                    continue;
                if ((*miter).first == addr)
                {
                    ostringstream s;
                    s << "Possible PIC construction at ";
                    op.getAddr().printRaw(s);
                    s << ": Changing call to branch";
                    data.warningHeader(s.str());
                    data.opSetOpcode(op, CPUI_BRANCH);
                    // Make sure target of new goto starts a basic block
                    PcodeOp* targ = target(addr);
                    data.opMarkStartBasic(targ);
                    // Make sure the following op starts a basic block
                    list<PcodeOp*>::const_iterator oiter = op.getInsertIter();
                    ++oiter;
                    if (oiter != obank.endDead())
                        data.opMarkStartBasic(*oiter);
                    // Restore original address
                    data.opSetInput(op, data.newCodeRef(addr), 0);
                    iter = qlst.erase(iter);    // Delete the call
                    delete fc;
                    if (iter == qlst.end()) break;
                }
                else
                {
                    data.warning("Call to offcut address within same function", op.getAddr());
                }
            }
        }

        /// \brief Look for changes in control-flow near indirect jumps that were discovered \e after the jumptable recovery
        private void checkMultistageJumptables()
        {
            int num = data.numJumpTables();
            for (int i = 0; i < num; ++i)
            {
                JumpTable* jt = data.getJumpTable(i);
                if (jt.checkForMultistage(&data))
                    tablelist.Add(jt.getIndirectOp());
            }
        }

        /// \brief Recover jumptables for the current set of BRANCHIND ops using existing flow
        ///
        /// This method passes back a list of JumpTable objects, one for each BRANCHIND in the current
        /// \b tablelist where the jumptable can be recovered. If a particular BRANCHIND cannot be recovered
        /// because the current partial control flow cannot legally reach it, the BRANCHIND is passed back
        /// in a separate list.
        /// \param newTables will hold the list of recovered JumpTables
        /// \param notreached will hold the list of BRANCHIND ops that could not be reached
        private void recoverJumpTables(List<JumpTable> newTables, List<PcodeOp> notreached)
        {
            PcodeOp* op = tablelist[0];
            ostringstream s1;
            s1 << data.getName() << "@@jump@";
            op.getAddr().printRaw(s1);

            string nm = s1.str();
            // Prepare partial Funcdata object for analysis if necessary
            Funcdata partial = new Funcdata(nm, nm, data.getScopeLocal().getParent(), data.getAddress(),
                (FunctionSymbol*)0);

            for (int i = 0; i < tablelist.size(); ++i)
            {
                op = tablelist[i];
                int failuremode;
                JumpTable* jt = data.recoverJumpTable(partial, op, this, failuremode); // Recover it
                if (jt == (JumpTable*)0)
                { // Could not recover jumptable
                    if ((failuremode == 3) && (tablelist.size() > 1) && (!isInArray(notreached, op)))
                    {
                        // If the indirect op was not reachable with current flow AND there is more flow to generate,
                        //     AND we haven't tried to recover this table before
                        notreached.Add(op); // Save this op so we can try to recovery table again later
                    }
                    else if (!isFlowForInline())    // Unless this flow is being inlined for something else
                        truncateIndirectJump(op, failuremode); // Treat the indirect jump as a call
                }
                newTables.Add(jt);
            }
        }

        /// Remove the given call site from the list for \b this function
        /// \param fc is the given call site (which is freed by this method)
        private void deleteCallSpec(FuncCallSpecs fc)
        {
            int i;
            for (i = 0; i < qlst.size(); ++i)
                if (qlst[i] == fc) break;

            if (i == qlst.size())
                throw new LowlevelError("Misplaced callspec");

            delete fc;
            qlst.erase(qlst.begin() + i);
        }

        /// Treat indirect jump as indirect call that never returns
        /// \param op is the BRANCHIND operation to convert
        /// \param failuremode is a code indicating the type of failure when trying to recover the jump table
        private void truncateIndirectJump(PcodeOp op, int failuremode)
        {
            data.opSetOpcode(op, CPUI_CALLIND); // Turn jump into call
            setupCallindSpecs(op, (FuncCallSpecs*)0);
            if (failuremode != 2)                   // Unless the switch was a thunk mechanism
                data.getCallSpecs(op).setBadJumpTable(true);   // Consider using special name for switch variable

            // Create an artificial return
            PcodeOp* truncop = artificialHalt(op.getAddr(), 0);
            data.opDeadInsertAfter(truncop, op);

            data.warning("Treating indirect jump as call", op.getAddr());
        }

        /// \brief Test if the given p-code op is a member of an array
        ///
        /// \param array is the array of p-code ops to search
        /// \param op is the given p-code op to search for
        /// \return \b true if the op is a member of the array
        private static bool isInArray(List<PcodeOp> array, PcodeOp op)
        {
            for (int i = 0; i < array.size(); ++i)
            {
                if (array[i] == op) return true;
            }
            return false;
        }

        /// Prepare for tracing flow for a new function.
        /// The Funcdata object and references to its internal containers must be explicitly given.
        /// \param d is the new function to trace
        /// \param o is the internal p-code container for the function
        /// \param b is the internal basic block container
        /// \param q is the internal container of call sites
        public FlowInfo(Funcdata d, PcodeOpBank o, BlockGraph b, List<FuncCallSpecs> q)
        {
            data = d;
            obank = o;
            bblocks = b;
            qlst = q;
            baddr = new Address(d.getAddress().getSpace(), 0);
            eaddr = new Address(d.getAddress().getSpace(), ~((ulong)0));
            minaddr = new Address(d.getAddress());
            maxaddr = new Address(d.getAddress());
            glb = data.getArch();
            flags = 0;
            emitter.setFuncdata(&d);
            inline_head = (Funcdata)null;
            inline_recursion = (set<Address>*)0;
            insn_count = 0;
            insn_max = uint.MaxValue;
            flowoverride_present = data.getOverride().hasFlowOverride();
        }

        /// Cloning constructor
        /// Prepare a new flow cloned from an existing flow.
        /// Configuration from the existing flow is copied, but the actual PcodeOps must already be
        /// cloned within the new function.
        /// \param d is the new function that has been cloned
        /// \param o is the internal p-code container for the function
        /// \param b is the internal basic block container
        /// \param q is the internal container of call sites
        /// \param op2 is the existing flow
        public FlowInfo(Funcdata d, PcodeOpBank o, BlockGraph b, List<FuncCallSpecs> q,
            FlowInfo op2)
        {
            data = d;
            obank = o;
            bblocks = b;
            qlst = q;
            baddr = new Address(op2.baddr);
            eaddr = new Address(op2.eaddr);
            minaddr = new Address(d.getAddress());
            maxaddr = new Address(d.getAddress());

            glb = data.getArch();
            flags = op2.flags;
            emitter.setFuncdata(&d);
            unprocessed = op2.unprocessed; // Clone the flow address information
            addrlist = op2.addrlist;
            visited = op2.visited;
            inline_head = op2.inline_head;
            if (inline_head != (Funcdata)null)
            {
                inline_base = op2.inline_base;
                inline_recursion = &inline_base;
            }
            else
                inline_recursion = (set<Address>*)0;
            insn_count = op2.insn_count;
            insn_max = op2.insn_max;
            flowoverride_present = data.getOverride().hasFlowOverride();
        }

        /// Establish the flow bounds
        public void setRange(Address b, Address e)
        {
            baddr = b;
            eaddr = e;
        }

        /// Set the maximum number of instructions
        public void setMaximumInstructions(uint max)
        {
            insn_max = max;
        }

        /// Enable a specific option
        public void setFlags(uint val)
        {
            flags |= val;
        }

        /// Disable a specific option
        public void clearFlags(uint val)
        {
            flags &= ~val;
        }

        /// Return first p-code op for instruction at given address
        /// The first p-code op associated with the machine instruction at the
        /// given address is returned.  If the instruction generated no p-code,
        /// an attempt is made to fall-thru to the next instruction.
        /// If no p-code op is ultimately found, an exception is thrown.
        /// \param addr is the given address of the instruction
        /// \return the targetted p-code op
        public PcodeOp target(Address addr)
        {
            map<Address, VisitStat>::const_iterator iter;

            iter = visited.find(addr);
            while (iter != visited.end())
            {
                SeqNum seq = (*iter).second.seqnum;
                if (!seq.getAddr().isInvalid())
                {
                    PcodeOp* retop = obank.findOp(seq);
                    if (retop != (PcodeOp)null)
                        return retop;
                    break;
                }
                // Visit fall thru address in case of no-op
                iter = visited.find((*iter).first + (*iter).second.size);
            }
            ostringstream errmsg;
            errmsg << "Could not find op at target address: (";
            errmsg << addr.getSpace().getName() << ',';
            addr.printRaw(errmsg);
            errmsg << ')';
            throw new LowlevelError(errmsg.str());
        }

        /// Find the target referred to by a given BRANCH or CBRANCH
        /// The \e code \e reference passed as the first parameter to the branch
        /// is examined, and the p-code op it refers to is returned.
        /// The reference may be a normal direct address or a relative offset.
        /// If no target p-code can be found, an exception is thrown.
        /// \param op is the given branch op
        /// \return the targetted p-code op
        public PcodeOp branchTarget(PcodeOp op)
        {
            Address addr = op.getIn(0).getAddr();
            if (addr.isConstant())
            {   // This is a relative sequence number
                Address res;
                PcodeOp* retop = findRelTarget(op, res);
                if (retop != (PcodeOp)null)
                    return retop;
                return target(res);
            }
            return target(addr);    // Otherwise a normal address target
        }

        /// Generate raw control-flow from the function's base address
        public void generateOps()
        {
            List<PcodeOp*> notreached;    // indirect ops that are not reachable
            int notreachcnt = 0;
            clearProperties();
            addrlist.Add(data.getAddress());
            while (!addrlist.empty())   // Recovering as much as possible except jumptables
                fallthru();
            if (hasInject())
                injectPcode();
            do
            {
                bool collapsed_jumptable = false;
                while (!tablelist.empty())
                {   // For each jumptable found
                    List<JumpTable*> newTables;
                    recoverJumpTables(newTables, notreached);
                    tablelist.clear();
                    for (int i = 0; i < newTables.size(); ++i)
                    {
                        JumpTable* jt = newTables[i];
                        if (jt == (JumpTable*)0) continue;

                        int num = jt.numEntries();
                        for (int i = 0; i < num; ++i)
                            newAddress(jt.getIndirectOp(), jt.getAddressByIndex(i));
                        if (jt.isPossibleMultistage())
                            collapsed_jumptable = true;
                        while (!addrlist.empty())   // Try to fill in as much more as possible
                            fallthru();
                    }
                }

                checkContainedCall();   // Check for PIC constructions
                if (collapsed_jumptable)
                    checkMultistageJumptables();
                while (notreachcnt < notreached.size())
                {
                    tablelist.Add(notreached[notreachcnt]);
                    notreachcnt += 1;
                }
                if (hasInject())
                    injectPcode();
            } while (!tablelist.empty());   // Inlining or multistage may have added new indirect branches
        }

        /// Generate basic blocks from the raw control-flow
        public void generateBlocks()
        {
            fillinBranchStubs();
            collectEdges();
            splitBasic();       // Split ops up into basic blocks
            connectBasic();     // Generate edges between basic blocks
            if (bblocks.getSize() != 0)
            {
                FlowBlock* startblock = bblocks.getBlock(0);
                if (startblock.sizeIn() != 0)
                { // Make sure the entry block has no incoming edges

                    // If it does we create a new entry block that flows into the old entry block
                    BlockBasic* newfront = bblocks.newBlockBasic(&data);
                    bblocks.addEdge(newfront, startblock);
                    bblocks.setStartBlock(newfront);
                    data.setBasicBlockRange(newfront, data.getAddress(), data.getAddress());
                }
            }

            if (hasPossibleUnreachable())
                data.removeUnreachableBlocks(false, true);
        }

        /// \brief For in-lining using the \e hard model, make sure some restrictions are met
        ///
        ///   - Can only in-line the function once.
        ///   - There must be a p-code op to return to.
        ///   - There must be a distinct return address, so that the RETURN can be replaced with a BRANCH.
        ///
        /// Pass back the distinct return address, unless the in-lined function doesn't return.
        /// \param inlinefd is the function being in-lined into \b this flow
        /// \param op is CALL instruction at the site of the in-line
        /// \param retaddr holds the passed back return address
        /// \return \b true if all the \e hard model restrictions are met
        public bool testHardInlineRestrictions(Funcdata inlinefd, PcodeOp op, Address retaddr)
        {
            if (inline_recursion.find(inlinefd.getAddress()) != inline_recursion.end())
            {
                // This function has already been included with current inlining
                inline_head.warning("Could not inline here", op.getAddr());
                return false;
            }

            if (!inlinefd.getFuncProto().isNoReturn())
            {
                list<PcodeOp*>::iterator iter = op.getInsertIter();
                ++iter;
                if (iter == obank.endDead())
                {
                    inline_head.warning("No fallthrough prevents inlining here", op.getAddr());
                    return false;
                }
                PcodeOp* nextop = *iter;
                retaddr = nextop.getAddr();
                if (op.getAddr() == retaddr)
                {
                    inline_head.warning("Return address prevents inlining here", op.getAddr());
                    return false;
                }
                // If the inlining "jumps back" this starts a new basic block
                data.opMarkStartBasic(nextop);
            }

            inline_recursion.insert(inlinefd.getAddress());
            return true;
        }

        /// Check if \b this flow matches the EX in-lining model
        /// A function is in the EZ model if it is a straight-line leaf function.
        /// \return \b true if this flow contains no CALL or BRANCH ops
        public bool checkEZModel()
        {
            list<PcodeOp*>::const_iterator iter = obank.beginDead();
            while (iter != obank.endDead())
            {
                PcodeOp* op = *iter;
                if (op.isCallOrBranch()) return false;
                ++iter;
            }
            return true;
        }

        /// Perform substitution on any op that requires \e injection
        /// Types of substitution include:
        ///   - Sub-function in-lining
        ///   - Sub-function injection
        ///   - User defined op injection
        ///
        /// Make sure to truncate recursion, and otherwise don't
        /// allow a sub-function to be in-lined more than once.
        public void injectPcode()
        {
            if (inline_head == (Funcdata)null)
            {
                // This is the top level of inlining
                inline_head = &data;    // Set up head of inlining
                inline_recursion = &inline_base;
                inline_recursion.insert(data.getAddress()); // Insert ourselves
                                                             //    inline_head = (Funcdata *)0;
            }
            else
            {
                inline_recursion.insert(data.getAddress()); // Insert ourselves
            }

            for (int i = 0; i < injectlist.size(); ++i)
            {
                PcodeOp* op = injectlist[i];
                if (op == (PcodeOp)null) continue;
                injectlist[i] = (PcodeOp)null;    // Nullify entry, so we don't inject more than once
                if (op.code() == CPUI_CALLOTHER)
                {
                    injectUserOp(op);
                }
                else
                {   // CPUI_CALL or CPUI_CALLIND
                    FuncCallSpecs* fc = FuncCallSpecs::getFspecFromConst(op.getIn(0).getAddr());
                    if (fc.isInline())
                    {
                        if (fc.getInjectId() >= 0)
                        {
                            if (injectSubFunction(fc))
                            {
                                data.warningHeader("Function: " + fc.getName() + " replaced with injection: " +
                                           glb.pcodeinjectlib.getCallFixupName(fc.getInjectId()));
                                deleteCallSpec(fc);
                            }
                        }
                        else if (inlineSubFunction(fc))
                        {
                            data.warningHeader("Inlined function: " + fc.getName());
                            deleteCallSpec(fc);
                        }
                    }
                }
            }
            injectlist.clear();
        }

        /// Pull in-lining recursion information from another flow
        /// When preparing p-code for an in-lined function, the generation process needs
        /// to be informed of in-lining that has already been performed.
        /// This method copies the in-lining information from the parent flow, prior to p-code generation.
        /// \param op2 is the parent flow
        public void forwardRecursion(FlowInfo op2)
        {
            inline_recursion = op2.inline_recursion;
            inline_head = op2.inline_head;
        }

        /// \brief Clone the given in-line flow into \b this flow using the \e hard model
        ///
        /// Individual PcodeOps from the Funcdata being in-lined are cloned into
        /// the Funcdata for \b this flow, preserving their original address.
        /// Any RETURN op is replaced with jump to first address following the call site.
        /// \param inlineflow is the given in-line flow to clone
        /// \param retaddr is the first address after the call site in \b this flow
        public void inlineClone(FlowInfo inlineflow, Address retaddr)
        {
            list<PcodeOp*>::const_iterator iter;
            for (iter = inlineflow.data.beginOpDead(); iter != inlineflow.data.endOpDead(); ++iter)
            {
                PcodeOp* op = *iter;
                PcodeOp* cloneop;
                if ((op.code() == CPUI_RETURN) && (!retaddr.isInvalid()))
                {
                    cloneop = data.newOp(1, op.getSeqNum());
                    data.opSetOpcode(cloneop, CPUI_BRANCH);
                    Varnode* vn = data.newCodeRef(retaddr);
                    data.opSetInput(cloneop, vn, 0);
                }
                else
                    cloneop = data.cloneOp(op, op.getSeqNum());
                if (cloneop.isCallOrBranch())
                    xrefInlinedBranch(cloneop);
            }
            // Copy in the cross-referencing
            unprocessed.insert(unprocessed.end(), inlineflow.unprocessed.begin(),
                       inlineflow.unprocessed.end());
            addrlist.insert(addrlist.end(), inlineflow.addrlist.begin(),
                    inlineflow.addrlist.end());
            visited.insert(inlineflow.visited.begin(), inlineflow.visited.end());
            // We don't copy inline_recursion or inline_head here
        }

        /// \brief Clone the given in-line flow into \b this flow using the EZ model
        ///
        /// Individual PcodeOps from the Funcdata being in-lined are cloned into
        /// the Funcdata for \b this flow but are reassigned a new fixed address,
        /// and the RETURN op is eliminated.
        /// \param inlineflow is the given in-line flow to clone
        /// \param calladdr is the fixed address assigned to the cloned PcodeOps
        public void inlineEZClone(FlowInfo inlineflow, Address calladdr)
        {
            list<PcodeOp*>::const_iterator iter;
            for (iter = inlineflow.data.beginOpDead(); iter != inlineflow.data.endOpDead(); ++iter)
            {
                PcodeOp* op = *iter;
                if (op.code() == CPUI_RETURN) break;
                SeqNum myseq(calladdr, op.getSeqNum().getTime());
                data.cloneOp(op, myseq);
            }
            // Because we are processing only straightline code and it is all getting assigned to one
            // address, we don't touch unprocessed, addrlist, or visited
        }

        /// Get the number of bytes covered by the flow
        public int getSize() => (int)(maxaddr.getOffset() - minaddr.getOffset());

        /// Does \b this flow have injections
        public bool hasInject() => !injectlist.empty();

        /// Does \b this flow have unimiplemented instructions
        public bool hasUnimplemented() => ((flags & unimplemented_present)!= 0);

        /// Does \b this flow reach inaccessible data
        public bool hasBadData() => ((flags & baddata_present)!= 0);

        /// Does \b this flow out of bound
        public bool hasOutOfBounds() => ((flags & outofbounds_present)!= 0);

        /// Does \b this flow reinterpret bytes
        public bool hasReinterpreted() => ((flags & reinterpreted_present)!= 0);

        /// Does \b this flow have too many instructions
        public bool hasTooManyInstructions() => ((flags & toomanyinstructions_present)!= 0);

        /// Is \b this flow to be in-lined
        public bool isFlowForInline() => ((flags & flow_forinline)!= 0);

        /// Should jump table structure be recorded
        public bool doesJumpRecord() => ((flags & record_jumploads)!= 0);
    }
}
