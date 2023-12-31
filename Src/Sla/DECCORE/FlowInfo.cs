﻿using Sla.CORE;
using Sla.EXTRA;

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
        private List<FuncCallSpecs> qlst; // Initialized in constructors
        /// PCodeOp factory (configured to allocate into \b data and \b obank)
        private PcodeEmitFd emitter;
        /// Addresses which are permanently unprocessed
        private List<Address> unprocessed;
        /// Addresses to which there is flow
        private List<Address> addrlist = new List<Address>();
        /// List of BRANCHIND ops (preparing for jump table recovery)
        private List<PcodeOp> tablelist = new List<PcodeOp>();
        /// List of p-code ops that need injection
        private List<PcodeOp> injectlist = new List<PcodeOp>();
        /// Map of machine instructions that have been visited so far
        private SortedList<Address, VisitStat> visited; // Initialized in constructors
        /// Source p-code op (Edges between basic blocks)
        private List<PcodeOp> block_edge1 = new List<PcodeOp>();
        /// Destination p-code op (Edges between basic blocks)
        private List<PcodeOp> block_edge2 = new List<PcodeOp>();
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
        private FlowFlag flags;
        /// First function in the in-lining chain
        private Funcdata? inline_head;
        /// Active list of addresses for function that are in-lined
        private HashSet<Address>? inline_recursion = null;
        /// Storage for addresses of functions that are in-lined
        private HashSet<Address>? inline_base = null;

        /// Are there possible unreachable ops
        private bool hasPossibleUnreachable() => ((flags & FlowFlag.possible_unreachable)!=0);

        /// Mark that there may be unreachable ops
        private void setPossibleUnreachable()
        {
            flags |= FlowFlag.possible_unreachable;
        }

        /// Clear any discovered flow properties
        private void clearProperties()
        {
            flags &= ~(FlowFlag.unimplemented_present | FlowFlag.baddata_present | FlowFlag.outofbounds_present);
            insn_count = 0;
        }

        /// Has the given instruction (address) been seen in flow
        private bool seenInstruction(Address addr)
        {
            return visited.ContainsKey(addr);
        }

        /// Find fallthru pcode-op for given op
        /// For efficiency, this method assumes the given op can actually fall-thru.
        /// \param op is the given PcodeOp
        /// \return the PcodeOp that fall-thru flow would reach (or NULL if there is no possible p-code op)
        private PcodeOp? fallthruOp(PcodeOp op)
        {
            PcodeOp retop;
            // IEnumerator<PcodeOp> iter = op.getInsertIter();
            LinkedListNode<PcodeOp>? iter = op.getInsertIter() ?? throw new ApplicationException();
            iter = iter.Next;
            if (null != iter) {
                retop = iter.Value;
                if (!retop.isInstructionStart()) // If within same instruction
                    return retop;       // Then this is the fall thru
            }
            // Find address of instruction containing this op
            int /*Dictionary<Address, VisitStat>.Enumerator*/ miter = visited.upper_bound(op.getAddr());
            if (miter == 0) return (PcodeOp)null;
            --miter;
            KeyValuePair<Address, VisitStat> currentlyVisited = visited.ElementAt(miter);
            if (currentlyVisited.Key + currentlyVisited.Value.size <= op.getAddr())
                return (PcodeOp)null;
            return target(currentlyVisited.Key + currentlyVisited.Value.size);
        }

        /// Register a new (non fall-thru) flow target
        /// Check to see if the new target has been seen before. Otherwise
        /// add it to the list of addresses that need to be processed.
        /// Also check range bounds and update basic block information.
        /// \param from is the PcodeOp issuing the branch
        /// \param to is the target address of the branch
        private void newAddress(PcodeOp from, Address to)
        {
            if ((to < baddr) || (eaddr < to)) {
                handleOutOfBounds(from.getAddr(), to);
                unprocessed.Add(to);
                return;
            }

            if (seenInstruction(to)) {
                // If we have seen this address before
                PcodeOp op = target(to);
                data.opMarkStartBasic(op);
                return;
            }
            addrlist.Add(to);
        }

        /// \brief Delete any remaining ops at the end of the instruction
        /// (because they have been predetermined to be dead)
        /// \param oiter is the point within the raw p-code list where deletion should start
        private void deleteRemainingOps(IEnumerator<PcodeOp> oiter)
        {
            do {
                PcodeOp op = oiter.Current;
                data.opDestroyRaw(op);
            } while (oiter.MoveNext());
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
        private PcodeOp xrefControlFlow(IEnumerator<PcodeOp> oiter, bool startbasic, bool isfallthru,
            FuncCallSpecs fc)
        {
            PcodeOp? op = (PcodeOp)null;
            isfallthru = false;
            uint maxtime = 0;  // Deepest internal relative branch
            bool loopCompleted = false;
            while (!loopCompleted && (oiter != obank.endDead())) {
                op = oiter.Current;
                loopCompleted = !oiter.MoveNext();
                if (startbasic) {
                    data.opMarkStartBasic(op);
                    startbasic = false;
                }
                switch (op.code()) {
                    case OpCode.CPUI_CBRANCH:
                        {
                            Address destaddr = op.getIn(0).getAddr();
                            if (destaddr.isConstant()) {
                                Address fallThruAddr;
                                PcodeOp? destop = findRelTarget(op, out fallThruAddr);
                                if (destop != (PcodeOp)null) {
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
                    case OpCode.CPUI_BRANCH:
                        {
                            Address destaddr = op.getIn(0).getAddr();
                            if (destaddr.isConstant()) {
                                Address fallThruAddr;
                                PcodeOp? destop = findRelTarget(op, out fallThruAddr);
                                if (destop != (PcodeOp)null) {
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
                            if (op.getTime() >= maxtime) {
                                deleteRemainingOps(oiter);
                                loopCompleted = true;
                            }
                            startbasic = true;
                        }
                        break;
                    case OpCode.CPUI_BRANCHIND:
                        tablelist.Add(op);    // Put off trying to recover the table
                        if (op.getTime() >= maxtime) {
                            deleteRemainingOps(oiter);
                            loopCompleted = true;
                        }
                        startbasic = true;
                        break;
                    case OpCode.CPUI_RETURN:
                        if (op.getTime() >= maxtime) {
                            deleteRemainingOps(oiter);
                            loopCompleted = true;
                        }
                        startbasic = true;
                        break;
                    case OpCode.CPUI_CALL:
                        if (setupCallSpecs(op, fc))
                            // Backup one op, to pickup halt
                            --oiter;
                        break;
                    case OpCode.CPUI_CALLIND:
                        if (setupCallindSpecs(op, fc))
                            // Backup one op, to pickup halt
                            --oiter;
                        break;
                    case OpCode.CPUI_CALLOTHER:
                        {
                            InjectedUserOp? userop = (glb.userops.getOp((int)op.getIn(0).getOffset())) as InjectedUserOp;
                            if (userop != (InjectedUserOp)null)
                                injectlist.Add(op);
                            break;
                        }
                    default:
                        break;
                }
            }
            if (isfallthru)     // We have seen an explicit relative branch to end of instruction
                startbasic = true;      // So we know next instruction starts a basicblock
            else {
                // If we haven't seen a relative branch, calculate fallthru by looking at last op
                if (op == (PcodeOp)null)
                    isfallthru = true;  // No ops at all, mean a fallthru
                else {
                    switch (op.code()) {
                        case OpCode.CPUI_BRANCH:
                        case OpCode.CPUI_BRANCHIND:
                        case OpCode.CPUI_RETURN:
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
            IEnumerator<PcodeOp>? oiter = null;
            int step;
            Override.Branching flowoverride;

            if (insn_count >= insn_max) {
                if ((flags & FlowFlag.error_toomanyinstructions) != 0)
                    throw new LowlevelError("Flow exceeded maximum allowable instructions");
                else {
                    step = 1;
                    artificialHalt(curaddr, PcodeOp.Flags.badinstruction);
                    data.warning("Too many instructions -- Truncating flow here", curaddr);
                    if (!hasTooManyInstructions()) {
                        flags |= FlowFlag.toomanyinstructions_present;
                        data.warningHeader("Exceeded maximum allowable instructions: Some flow is truncated");
                    }
                }
            }
            insn_count += 1;

            if (obank.empty())
                emptyflag = true;
            else {
                emptyflag = false;
                oiter = obank.endDead();
                --oiter;
            }
            if (flowoverride_present)
                flowoverride = data.getOverride().getFlowOverride(curaddr);
            else
                flowoverride = Override.Branching.NONE;

            try {
                step = glb.translate.oneInstruction(emitter, curaddr); // Generate ops for instruction
            }
            catch (UnimplError err) {
                // Instruction is unimplemented
                if ((flags & FlowFlag.ignore_unimplemented) != 0) {
                    step = err.instruction_length;
                    if (!hasUnimplemented()) {
                        flags |= FlowFlag.unimplemented_present;
                        data.warningHeader("Control flow ignored unimplemented instructions");
                    }
                }
                else if ((flags & FlowFlag.error_unimplemented) != 0)
                    throw err;      // rethrow
                else {
                    // Add infinite loop instruction
                    step = 1;           // Pretend size 1
                    artificialHalt(curaddr, PcodeOp.Flags.unimplemented);
                    data.warning("Unimplemented instruction - Truncating control flow here", curaddr);
                    if (!hasUnimplemented()) {
                        flags |= FlowFlag.unimplemented_present;
                        data.warningHeader("Control flow encountered unimplemented instructions");
                    }
                }
            }
            catch (BadDataError err) {
                if ((flags & FlowFlag.error_unimplemented) != 0)
                    // rethrow
                    throw err;
                // Add infinite loop instruction
                // Pretend size 1
                step = 1;
                artificialHalt(curaddr, PcodeOp.Flags.badinstruction);
                data.warning("Bad instruction - Truncating control flow here", curaddr);
                if (!hasBadData()) {
                    flags |= FlowFlag.baddata_present;
                    data.warningHeader("Control flow encountered bad instruction data");
                }
            }
            // Mark that we visited this instruction
            VisitStat stat = visited[curaddr];
            // Record size of instruction
            stat.size = step;

            if (curaddr < minaddr)
                // Update minimum and maximum address
                minaddr = curaddr;
            if (maxaddr < curaddr + step)
                // Keep track of biggest and smallest address
                maxaddr = curaddr + step;

            if (emptyflag)
                // Make sure oiter points at first new op
                oiter = obank.beginDead();
            else
                (oiter ?? throw new ApplicationException()).MoveNext();

            if (oiter != obank.endDead()) {
                stat.seqnum = oiter.Current.getSeqNum();
                // Mark the first op in the instruction
                data.opMarkStartInstruction(oiter.Current);
                if (flowoverride != Override.Branching.NONE)
                    data.overrideFlow(curaddr, flowoverride);
                xrefControlFlow(oiter, startbasic, isfallthru, (FuncCallSpecs)null);
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
            Address bound = new Address();

            if (!setFallthruBound(bound)) return;

            Address curaddr;
            bool startbasic = true;
            bool fallthruflag;

            while(true) {
                curaddr = addrlist.GetLastItem();
                addrlist.RemoveLastItem();
                fallthruflag = processInstruction(curaddr, startbasic);
                if (!fallthruflag) break;
                if (addrlist.empty()) break;
                if (bound <= addrlist.GetLastItem()) {
                    if (bound == eaddr) {
                        handleOutOfBounds(eaddr, addrlist.GetLastItem());
                        unprocessed.Add(addrlist.GetLastItem());
                        addrlist.RemoveLastItem();
                        return;
                    }
                    if (bound == addrlist.GetLastItem()) {
                        // Hit the bound exactly
                        if (startbasic) {
                            PcodeOp op = target(addrlist.GetLastItem());
                            data.opMarkStartBasic(op);
                        }
                        addrlist.RemoveLastItem();
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
        private PcodeOp? findRelTarget(PcodeOp op, out Address? res)
        {
            Address addr = op.getIn(0).getAddr();
            uint id = (uint)(op.getTime() + addr.getOffset());
            SeqNum seqnum = new SeqNum(op.getAddr(), id);
            PcodeOp? retop = obank.findOp(seqnum);
            if (retop != (PcodeOp)null) {
                // Is this a "properly" internal branch
                res = null;
                return retop;
            }

            // Now we check if the relative branch is really to the next instruction
            SeqNum seqnum1 = new SeqNum(op.getAddr(), id-1);
            retop = obank.findOp(seqnum1); // We go back one sequence number
            if (retop != (PcodeOp)null) {
                // If the PcodeOp exists here then branch was indeed to next instruction
                int /*Dictionary<Address, VisitStat>.Enumerator*/ miter = visited.upper_bound(retop.getAddr());
                if (miter != 0) {
                    --miter;
                    KeyValuePair<Address, VisitStat> currentlyVisited = visited.ElementAt(miter);
                    res = currentlyVisited.Key + currentlyVisited.Value.size;
                    if (op.getAddr() < res) {
                        // Indicate that res has the fallthru address
                        return (PcodeOp)null;
                    }
                }
            }
            StringWriter errmsg = new StringWriter();
            errmsg.Write($"Bad relative branch at instruction : ({op.getAddr().getSpace().getName()},");
            op.getAddr().printRaw(errmsg);
            throw new LowlevelError((errmsg + ")").ToString());
        }

        /// Add any remaining un-followed addresses to the \b unprocessed list
        /// In the case where additional flow is truncated, run through the list of
        /// pending addresses, and if they don't have a p-code generated for them,
        /// add the Address to the \b unprocessed array.
        private void findUnprocessed()
        {
            foreach (Address scannedAddress in addrlist) {
                if (seenInstruction(scannedAddress)) {
                    data.opMarkStartBasic(target(scannedAddress));
                }
                else
                    unprocessed.Add(scannedAddress);
            }
        }

        /// Get rid of duplicates in the \b unprocessed list
        /// The list is also sorted
        private void dedupUnprocessed()
        {
            if (0 == unprocessed.Count) {
                return;
            }
            unprocessed.Sort();
            int scanIndex = 0;
            int replaceIndex = 0;
            Address lastaddr = unprocessed[0];
            while (scanIndex < unprocessed.Count) {
                Address scannedAddress = unprocessed[scanIndex];
                if (scannedAddress == lastaddr) {
                    scanIndex++;
                }
                else {
                    lastaddr = scannedAddress;
                    scanIndex++;
                    unprocessed[replaceIndex] = lastaddr;
                    replaceIndex++;
                }
            }
            while(replaceIndex < unprocessed.Count) {
                unprocessed.RemoveAt(replaceIndex);
            }
        }

        /// Fill-in artificial HALT p-code for \b unprocessed addresses
        /// A special form of RETURN instruction is generated for every address in
        /// the \b unprocessed list.
        private void fillinBranchStubs()
        {
            findUnprocessed();
            dedupUnprocessed();
            foreach (Address scannedAddress in unprocessed) {
                PcodeOp op = artificialHalt(scannedAddress, PcodeOp.Flags.missing);
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
            PcodeOp op, targ_op;
            JumpTable jt;
            bool nextstart;
            int i, num;

            if (bblocks.getSize() != 0)
                throw new RecovError("Basic blocks already calculated\n");

            IEnumerator<PcodeOp> iter = obank.beginDead();
            bool loopCompleted = !iter.MoveNext();
            while (!loopCompleted) {
                op = iter.Current;
                if (!iter.MoveNext()) {
                    loopCompleted = true;
                    nextstart = true;
                }
                else {
                    nextstart = iter.Current.isBlockStart();
                }
                switch (op.code()) {
                    case OpCode.CPUI_BRANCH:
                        targ_op = branchTarget(op);
                        block_edge1.Add(op);
                        //      block_edge2.Add(op.Input(0).getAddr().Iop());
                        block_edge2.Add(targ_op);
                        break;
                    case OpCode.CPUI_BRANCHIND:
                        jt = data.findJumpTable(op);
                        if (jt == (JumpTable)null) break;
                        // If we are in this routine and there is no table
                        // Then we must be doing partial flow analysis
                        // so assume there are no branches out
                        num = jt.numEntries();
                        for (i = 0; i < num; ++i) {
                            targ_op = target(jt.getAddressByIndex(i));
                            if (targ_op.isMark()) continue; // Already a link between these blocks
                            targ_op.setMark();
                            block_edge1.Add(op);
                            block_edge2.Add(targ_op);
                        }
                        IEnumerator<PcodeOp> iter1 = block_edge1.GetReverseEnumerator(); // Clean up our marks
                        IEnumerator<PcodeOp> iter2 = block_edge2.GetReverseEnumerator();
                        while (iter1.MoveNext()) {
                            iter2.MoveNext();
                            if (iter1.Current == op)
                                iter2.Current.clearMark();
                            else
                                break;
                        }
                        break;
                    case OpCode.CPUI_RETURN:
                        break;
                    case OpCode.CPUI_CBRANCH:
                        targ_op = fallthruOp(op); // Put in fallthru edge
                        block_edge1.Add(op);
                        block_edge2.Add(targ_op);
                        targ_op = branchTarget(op);
                        block_edge1.Add(op);
                        block_edge2.Add(targ_op);
                        break;
                    default:
                        if (nextstart) {
                            // Put in fallthru edge if new basic block
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
            PcodeOp op;
            BlockBasic cur;

            IEnumerator<PcodeOp> iter = obank.beginDead();
            // IEnumerator<PcodeOp> iterend = obank.endDead();
            if (!iter.MoveNext()) return;
            op = iter.Current;
            if (!op.isBlockStart())
                throw new LowlevelError("First op not marked as entry point");
            cur = bblocks.newBlockBasic(data);
            data.opInsert(op, cur, null);
            bblocks.setStartBlock(cur);
            Address start = op.getAddr();
            Address stop = start;
            while (iter.MoveNext()) {
                op = iter.Current;
                if (op.isBlockStart()) {
                    data.setBasicBlockRange(cur, start, stop);
                    // Set up the next basic block
                    cur = bblocks.newBlockBasic(data);
                    start = op.getSeqNum().getAddr();
                    stop = start;
                }
                else {
                    Address nextAddr = op.getAddr();
                    if (stop < nextAddr)
                        stop = nextAddr;
                }
                data.opInsert(op, cur, null);
            }
            data.setBasicBlockRange(cur, start, stop);
        }

        /// Generate edges between basic blocks
        /// Directed edges between the PcodeBlockBasic objects are created based on the
        /// previously collected p-code op pairs in \b block_edge1 and \b block_edge2
        private void connectBasic()
        {
            BlockBasic bs;
            BlockBasic targ_bs;

            IEnumerator<PcodeOp> iter = block_edge1.GetEnumerator();
            IEnumerator<PcodeOp> iter2 = block_edge2.GetEnumerator();
            while (iter.MoveNext()) {
                PcodeOp op = iter.Current;
                if (!iter2.MoveNext()) throw new BugException();
                PcodeOp targ_op = iter2.Current;
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
            Address addr = addrlist.GetLastItem();

            // First range greater than addr
            int iter = visited.upper_bound(addr);
            if (iter != 0) {
                // Last range less than or equal to us
                --iter;
                KeyValuePair<Address, VisitStat> visitedItem = visited.ElementAt(iter);
                if (addr == visitedItem.Key) {
                    // If we have already visited this address
                    PcodeOp op = target(addr); // But make sure the address
                    data.opMarkStartBasic(op); // starts a basic block
                    addrlist.RemoveLastItem();    // Throw it away
                    return false;
                }
                if (addr < visitedItem.Key + visitedItem.Value.size) {
                    reinterpreted(addr);
                }
                iter++;
            }
            bound = (iter < visited.Count)
                // Whats the maximum distance we can go
                ? visited.ElementAt(iter).Key
                : eaddr;
            return true;
        }

        /// \brief Generate warning message or throw exception for given flow that is out of bounds
        ///
        /// \param fromaddr is the source address of the flow (presumably in bounds)
        /// \param toaddr is the given destination address that is out of bounds
        private void handleOutOfBounds(Address fromaddr, Address toaddr)
        {
            if ((flags & FlowFlag.ignore_outofbounds) == 0) {
                // Should we throw an error for out of bounds
                StringWriter errmsg = new StringWriter();
                errmsg.Write($"Function flow out of bounds: {fromaddr.getShortcut()}");
                fromaddr.printRaw(errmsg);
                errmsg.Write($" flows to {toaddr.getShortcut()}");
                toaddr.printRaw(errmsg);
                if ((flags & FlowFlag.error_outofbounds) == 0) {
                    data.warning(errmsg.ToString(), toaddr);
                    if (!hasOutOfBounds()) {
                        flags |= FlowFlag.outofbounds_present;
                        data.warningHeader("Function flows out of bounds");
                    }
                }
                else
                    throw new LowlevelError(errmsg.ToString());
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
        private PcodeOp artificialHalt(Address addr, PcodeOp.Flags flag)
        {
            PcodeOp haltop = data.newOp(1, addr);
            data.opSetOpcode(haltop, OpCode.CPUI_RETURN);
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
            int /*IEnumerator<KeyValuePair<Address, VisitStat>>*/ iter = visited.upper_bound(addr);
            if (iter == 0)
                // Should never happen
                return;
            --iter;
            Address addr2 = visited.ElementAt(iter).Key;
            StringWriter s = new StringWriter();

            s.Write($"Instruction at ({addr.getSpace().getName()},");
            addr.printRaw(s);
            s.Write($") overlaps instruction at ({addr2.getSpace().getName()},");
            addr2.printRaw(s);
            s.WriteLine(')');
            if ((flags & FlowFlag.error_reinterpreted) != 0) {
                throw new LowlevelError(s.ToString());
            }
            if ((flags & FlowFlag.reinterpreted_present) == 0) {
                flags |= FlowFlag.reinterpreted_present;
                data.warningHeader(s.ToString());
            }
        }

        /// \brief Check for modifications to flow at a call site given the recovered FuncCallSpecs
        /// The sub-function may be in-lined or never return.
        /// \param fspecs is the given call site
        /// \return \b true if the sub-function never returns
        private bool checkForFlowModification(FuncCallSpecs fspecs)
        {
            if (fspecs.isInline())
                injectlist.Add(fspecs.getOp());
            if (fspecs.isNoReturn()) {
                PcodeOp op = fspecs.getOp();
                PcodeOp haltop = artificialHalt(op.getAddr(), PcodeOp.Flags.noreturn);
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
            if (!fspecs.getEntryAddress().isInvalid()) {
                // If this is a direct call
                Funcdata? otherfunc = data.getScopeLocal().getParent().queryFunction(fspecs.getEntryAddress());
                if (otherfunc != (Funcdata)null) {
                    fspecs.setFuncdata(otherfunc); // Associate the symbol with the callsite
                    if (!fspecs.hasModel() || otherfunc.getFuncProto().isInline()) {
                        // If the prototype was not overridden
                        // Take the flow affects of the symbol
                        // If the call site is applying just the standard prototype from the symbol,
                        // this postpones the full copy of the prototype until ActionDefaultParams
                        // Which lets "last second" changes come in, between when the function is first walked and
                        // when it is finally decompiled
                        fspecs.copyFlowEffects(otherfunc.getFuncProto());
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
        private bool setupCallSpecs(PcodeOp op, FuncCallSpecs? fc)
        {
            FuncCallSpecs res = new FuncCallSpecs(op);
            data.opSetInput(op, data.newVarnodeCallSpecs(res), 0);
            qlst.Add(res);

            data.getOverride().applyPrototype(data, res);
            queryCall(res);
            if (fc != (FuncCallSpecs)null) {
                // If we are already in the midst of an injection
                if (fc.getEntryAddress() == res.getEntryAddress())
                    res.cancelInjectId();      // Don't allow recursion
            }
            return checkForFlowModification(res);
        }

        /// \brief Set up the FuncCallSpecs object for a new indirect call site
        ///
        /// The new FuncCallSpecs object is created and initialized based on
        /// the CALLIND op at the site. Any overriding prototype or control-flow may be examined and applied.
        /// \param op is the given CALLIND op
        /// \param fc is non-NULL if \e injection is in progress and a cycle check needs to be made
        /// \return \b true if it is discovered the sub-function never returns
        private bool setupCallindSpecs(PcodeOp op, FuncCallSpecs? fc)
        {
            FuncCallSpecs res = new FuncCallSpecs(op);
            qlst.Add(res);

            data.getOverride().applyIndirect(data, res);
            if (fc != (FuncCallSpecs)null && fc.getEntryAddress() == res.getEntryAddress())
                res.setAddress(new Address()); // Cancel any indirect override
            data.getOverride().applyPrototype(data, res);
            queryCall(res);

            if (!res.getEntryAddress().isInvalid()) {
                // If we are overridden to a direct call
                // Change indirect pcode call into a normal pcode call
                data.opSetOpcode(op, OpCode.CPUI_CALL); // Set normal opcode
                data.opSetInput(op, data.newVarnodeCallSpecs(res), 0);
            }
            return checkForFlowModification(res);
        }

        /// Check for control-flow in a new injected p-code op
        /// If the given injected op is a CALL, CALLIND, or BRANCHIND,
        /// we need to add references to it in other flow tables.
        /// \param op is the given injected p-code op
        private void xrefInlinedBranch(PcodeOp op)
        {
            if (op.code() == OpCode.CPUI_CALL)
                setupCallSpecs(op, (FuncCallSpecs)null);
            else if (op.code() == OpCode.CPUI_CALLIND)
                setupCallindSpecs(op, (FuncCallSpecs)null);
            else if (op.code() == OpCode.CPUI_BRANCHIND) {
                JumpTable? jt = data.linkJumpTable(op);
                if (jt == (JumpTable)null)
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
            IEnumerator<PcodeOp> iter = obank.beginReverseDead();

            payload.inject(icontext, emitter);     // Do the injection

            bool startbasic = op.isBlockStart();
            // There must be at least one op
            // Now points to first op in the injection
            if (!iter.MoveNext()) {
                throw new LowlevelError("Empty injection: " + payload.getName());
            }
            PcodeOp firstop = iter.Current;
            bool isfallthru = true;
            PcodeOp lastop = xrefControlFlow(iter, startbasic, isfallthru, fc);

            if (startbasic) {
                // If the inject code does NOT fall thru
                // iter = op.getInsertIter();
                LinkedListNode<PcodeOp>? scannedNode = op.getInsertIter() ?? throw new ApplicationException();
                // Mark next op after the call
                // ++iter;
                scannedNode = scannedNode.Next;
                if (null != scannedNode)
                    // as start of basic block
                    data.opMarkStartBasic(scannedNode.Value);
            }

            if (payload.isIncidentalCopy())
                obank.markIncidentalCopy(firstop, lastop);
            obank.moveSequenceDead(firstop, lastop, op); // Move the injection to right after the call

            VisitStat visitStatus;
            if (visited.TryGetValue(op.getAddr(), out visitStatus)) {
                // Check if -op- is a possible branch target
                if (visitStatus.seqnum == op.getSeqNum())
                    // (if injection op is the first op for its address)
                    // change the seqnum to the first injected op
                    visitStatus.seqnum = firstop.getSeqNum();
            }
            // Get rid of the original call
            data.opDestroyRaw(op);
        }

        /// Perform \e injection for a given user-defined p-code op
        /// The op must already be established as a user defined op with an associated injection
        /// \param op is the given PcodeOp
        private void injectUserOp(PcodeOp op)
        {
            InjectedUserOp userop = (InjectedUserOp)glb.userops.getOp((int)op.getIn(0).getOffset());
            InjectPayload payload = glb.pcodeinjectlib.getPayload((int)userop.getInjectId());
            InjectContext icontext = glb.pcodeinjectlib.getCachedContext();
            icontext.clear();
            icontext.baseaddr = op.getAddr();
            icontext.nextaddr = icontext.baseaddr;
            for (int i = 1; i < op.numInput(); ++i) {
                // Skip the first operand containing the injectid
                Varnode vn = op.getIn(i);
                icontext.inputlist.Add(new VarnodeData() {
                    space = vn.getSpace(),
                    offset = vn.getOffset(),
                    size = (uint)vn.getSize()
                });
            }
            Varnode? outvn = op.getOut();
            if (outvn != (Varnode)null) {
                icontext.output.Add(new VarnodeData() {
                    space = outvn.getSpace(),
                    offset = outvn.getOffset(),
                    size = (uint)outvn.getSize()
                });
            }
            doInjection(payload, icontext, op, (FuncCallSpecs)null);
        }

        /// In-line the sub-function at the given call site
        /// P-code is generated for the sub-function and then woven into \b this flow
        /// at the call site.
        /// \param fc is the given call site
        /// \return \b true if the in-lining is successful
        private bool inlineSubFunction(FuncCallSpecs fc)
        {
            Funcdata? fd = fc.getFuncdata();
            if (fd == (Funcdata)null) return false;
            PcodeOp op = fc.getOp();
            Address retaddr;

            if (!data.inlineFlow(fd, this, op))
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
            PcodeOp op = fc.getOp();

            // Inject to end of the deadlist
            InjectContext icontext = glb.pcodeinjectlib.getCachedContext();
            icontext.clear();
            icontext.baseaddr = op.getAddr();
            icontext.nextaddr = icontext.baseaddr;
            icontext.calladdr = fc.getEntryAddress();
            InjectPayload payload = glb.pcodeinjectlib.getPayload(fc.getInjectId());
            doInjection(payload, icontext, op, fc);
            // If the injection fills in the -paramshift- field of the context
            // pass this information on to the callspec of the injected call, which must be last in the list
            if (payload.getParamShift() != 0)
                qlst.GetLastItem().setParamshift(payload.getParamShift());

            return true;            // Return true to indicate injection happened and callspec should be deleted
        }

        /// \brief Check if any of the calls this function makes are to already traced data-flow.
        ///
        /// If so, we change the CALL to a BRANCH and issue a warning.
        /// This situation is most likely due to a Position Indepent Code construction.
        private void checkContainedCall()
        {
            for(int index = 0; index < qlst.Count; index++) {
                FuncCallSpecs fc = qlst[index];
                Funcdata? fd = fc.getFuncdata();
                if (fd != (Funcdata)null) continue;
                PcodeOp op = fc.getOp();
                if (op.code() != OpCode.CPUI_CALL) continue;

                Address addr = fc.getEntryAddress();
                int /*IEnumerator<KeyValuePair<Address, VisitStat>>*/ miter = visited.upper_bound(addr);
                if (miter == 0) {
                    continue;
                }
                --miter;
                KeyValuePair<Address, VisitStat> currentlyVisited = visited.ElementAt(miter);
                if (currentlyVisited.Key + currentlyVisited.Value.size <= addr)
                    continue;
                if (currentlyVisited.Key == addr) {
                    data.warningHeader(
                        "Possible PIC construction at {op.getAddr().printRaw(s)}: Changing call to branch");
                    data.opSetOpcode(op, OpCode.CPUI_BRANCH);
                    // Make sure target of new goto starts a basic block
                    PcodeOp targ = target(addr);
                    data.opMarkStartBasic(targ);
                    // Make sure the following op starts a basic block
                    LinkedListNode<PcodeOp>? oiter = op.getInsertIter() ?? throw new ApplicationException();
                    // First item is omited
                    if (null == (oiter = oiter.Next)) throw new BugException();
                    if (null != (oiter = oiter.Next)) 
                        data.opMarkStartBasic(oiter.Value);
                    // Restore original address
                    data.opSetInput(op, data.newCodeRef(addr), 0);
                    qlst.RemoveAt(index--);
                    // delete fc;
                    // if (iter == qlst.end()) break; // Handled in for loop
                }
                else {
                    data.warning("Call to offcut address within same function", op.getAddr());
                }
            }
        }

        /// \brief Look for changes in control-flow near indirect jumps that were discovered \e after the jumptable recovery
        private void checkMultistageJumptables()
        {
            int num = data.numJumpTables();
            for (int i = 0; i < num; ++i) {
                JumpTable jt = data.getJumpTable(i);
                if (jt.checkForMultistage(data))
                    tablelist.Add(jt.getIndirectOp());
            }
        }

        /// \brief Recover jumptables for the current set of BRANCHIND ops using existing flow
        /// This method passes back a list of JumpTable objects, one for each BRANCHIND in the current
        /// \b tablelist where the jumptable can be recovered. If a particular BRANCHIND cannot be recovered
        /// because the current partial control flow cannot legally reach it, the BRANCHIND is passed back
        /// in a separate list.
        /// \param newTables will hold the list of recovered JumpTables
        /// \param notreached will hold the list of BRANCHIND ops that could not be reached
        private void recoverJumpTables(List<JumpTable> newTables, List<PcodeOp> notreached)
        {
            PcodeOp op = tablelist[0];
            TextWriter s1 = new StringWriter();
            s1.Write($"{data.getName()}@@jump@");
            op.getAddr().printRaw(s1);

            string nm = s1.ToString();
            // Prepare partial Funcdata object for analysis if necessary
            Funcdata partial = new Funcdata(nm, nm, data.getScopeLocal().getParent(), data.getAddress(),
                (FunctionSymbol)null);

            for (int i = 0; i < tablelist.size(); ++i) {
                op = tablelist[i];
                int failuremode;
                JumpTable? jt = data.recoverJumpTable(partial, op, this, out failuremode); // Recover it
                if (jt == (JumpTable)null) {
                    // Could not recover jumptable
                    if ((failuremode == 3) && (tablelist.size() > 1) && (!isInArray(notreached, op))) {
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
            //int i;
            //for (i = 0; i < qlst.size(); ++i)
            //    if (qlst[i] == fc) break;

            //if (i == qlst.size())
            //    throw new LowlevelError("Misplaced callspec");

            //// delete fc;
            //qlst.RemoveAt(i);

            if (!qlst.Remove(fc))
                throw new LowlevelError("Misplaced callspec");
        }

        /// Treat indirect jump as indirect call that never returns
        /// \param op is the BRANCHIND operation to convert
        /// \param failuremode is a code indicating the type of failure when trying to recover the jump table
        private void truncateIndirectJump(PcodeOp op, int failuremode)
        {
            op.AssertIsIndirectBranching();
            data.opSetOpcode(op, OpCode.CPUI_CALLIND); // Turn jump into call
            setupCallindSpecs(op, (FuncCallSpecs)null);
            if (failuremode != 2)                   // Unless the switch was a thunk mechanism
                data.getCallSpecs(op).setBadJumpTable(true);   // Consider using special name for switch variable

            // Create an artificial return
            PcodeOp truncop = artificialHalt(op.getAddr(), 0);
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
            for (int i = 0; i < array.size(); ++i) {
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
            eaddr = new Address(d.getAddress().getSpace(), ulong.MaxValue);
            minaddr = d.getAddress();
            maxaddr = d.getAddress();
            glb = data.getArch();
            flags = 0;
            emitter.setFuncdata(d);
            inline_head = (Funcdata)null;
            inline_recursion = (HashSet<Address>)null;
            insn_count = 0;
            insn_max = uint.MaxValue;
            flowoverride_present = data.getOverride().hasFlowOverride();
            visited = new SortedList<Address, VisitStat>();
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
            baddr = op2.baddr;
            eaddr = op2.eaddr;
            minaddr = d.getAddress();
            maxaddr = d.getAddress();

            glb = data.getArch();
            flags = op2.flags;
            emitter.setFuncdata(d);
            unprocessed = op2.unprocessed; // Clone the flow address information
            addrlist = op2.addrlist;
            visited = op2.visited;
            inline_head = op2.inline_head;
            if (inline_head != (Funcdata)null) {
                inline_base = op2.inline_base;
                inline_recursion = inline_base;
            }
            else {
                inline_recursion = (HashSet<Address>)null;
            }
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
        public void setFlags(FlowInfo.FlowFlag val)
        {
            flags |= val;
        }

        /// Disable a specific option
        public void clearFlags(FlowInfo.FlowFlag val)
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
            //IEnumerator<KeyValuePair<Address, VisitStat>> iter;
            // iter = visited.find(addr);
            VisitStat visitationStatus;
            while (visited.TryGetValue(addr, out visitationStatus)) {
                SeqNum seq = visitationStatus.seqnum;
                if (!seq.getAddr().isInvalid()){
                    PcodeOp? retop = obank.findOp(seq);
                    if (retop != (PcodeOp)null)
                        return retop;
                    break;
                }
                // Visit fall thru address in case of no-op
                // iter = visited.find(iter.Current.Key + (*iter).second.size);
                addr = addr + visitationStatus.size;
            }
            TextWriter errmsg = new StringWriter();
            errmsg.Write($"Could not find op at target address: ({addr.getSpace().getName()},");
            addr.printRaw(errmsg);
            errmsg.Write(')');
            throw new LowlevelError(errmsg.ToString());
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
            if (addr.isConstant()) {
                // This is a relative sequence number
                Address res;
                PcodeOp? retop = findRelTarget(op, out res);
                return retop ?? target(res);
            }
            return target(addr);    // Otherwise a normal address target
        }

        /// Generate raw control-flow from the function's base address
        public void generateOps()
        {
            List<PcodeOp> notreached = new List<PcodeOp>();    // indirect ops that are not reachable
            int notreachcnt = 0;
            clearProperties();
            addrlist.Add(data.getAddress());
            while (!addrlist.empty())   // Recovering as much as possible except jumptables
                fallthru();
            if (hasInject())
                injectPcode();
            do {
                bool collapsed_jumptable = false;
                while (!tablelist.empty()) {
                    // For each jumptable found
                    List<JumpTable> newTables = new List<JumpTable>();
                    recoverJumpTables(newTables, notreached);
                    tablelist.Clear();
                    for (int i = 0; i < newTables.size(); ++i) {
                        JumpTable? jt = newTables[i];
                        if (jt == (JumpTable)null) continue;

                        int num = jt.numEntries();
                        for (int j = 0; i < num; ++i)
                            newAddress(jt.getIndirectOp(), jt.getAddressByIndex(j));
                        if (jt.isPossibleMultistage())
                            collapsed_jumptable = true;
                        while (!addrlist.empty())   // Try to fill in as much more as possible
                            fallthru();
                    }
                }

                checkContainedCall();   // Check for PIC constructions
                if (collapsed_jumptable)
                    checkMultistageJumptables();
                while (notreachcnt < notreached.size()) {
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
            if (bblocks.getSize() != 0) {
                FlowBlock startblock = bblocks.getBlock(0);
                if (startblock.sizeIn() != 0) {
                    // Make sure the entry block has no incoming edges
                    // If it does we create a new entry block that flows into the old entry block
                    BlockBasic newfront = bblocks.newBlockBasic(data);
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
            if (inline_recursion.Contains(inlinefd.getAddress())) {
                // This function has already been included with current inlining
                inline_head.warning("Could not inline here", op.getAddr());
                return false;
            }

            if (!inlinefd.getFuncProto().isNoReturn()) {
                // IEnumerator<PcodeOp> iter = op.getInsertIter();
                LinkedListNode<PcodeOp>? iter = op.getInsertIter() ?? throw new ApplicationException();
                iter = iter.Next;
                if (null == iter) {
                    inline_head.warning("No fallthrough prevents inlining here", op.getAddr());
                    return false;
                }
                PcodeOp nextop = iter.Value;
                retaddr = nextop.getAddr();
                if (op.getAddr() == retaddr) {
                    inline_head.warning("Return address prevents inlining here", op.getAddr());
                    return false;
                }
                // If the inlining "jumps back" this starts a new basic block
                data.opMarkStartBasic(nextop);
            }

            inline_recursion.Add(inlinefd.getAddress());
            return true;
        }

        /// Check if \b this flow matches the EX in-lining model
        /// A function is in the EZ model if it is a straight-line leaf function.
        /// \return \b true if this flow contains no CALL or BRANCH ops
        public bool checkEZModel()
        {
            IEnumerator<PcodeOp> iter = obank.beginDead();
            while (iter.MoveNext()) {
                PcodeOp op = iter.Current;
                if (op.isCallOrBranch()) return false;
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
            if (inline_head == (Funcdata)null) {
                // This is the top level of inlining
                inline_head = data;    // Set up head of inlining
                inline_recursion = inline_base;
                inline_recursion.Add(data.getAddress()); // Insert ourselves
                                                             //    inline_head = (Funcdata *)0;
            }
            else {
                inline_recursion.Add(data.getAddress()); // Insert ourselves
            }

            for (int i = 0; i < injectlist.size(); ++i) {
                PcodeOp op = injectlist[i];
                if (op == (PcodeOp)null) continue;
                injectlist[i] = (PcodeOp)null;    // Nullify entry, so we don't inject more than once
                if (op.code() == OpCode.CPUI_CALLOTHER) {
                    injectUserOp(op);
                }
                else {
                    // OpCode.CPUI_CALL or OpCode.CPUI_CALLIND
                    FuncCallSpecs fc = FuncCallSpecs.getFspecFromConst(op.getIn(0).getAddr());
                    if (fc.isInline()) {
                        if (fc.getInjectId() >= 0) {
                            if (injectSubFunction(fc)) {
                                data.warningHeader(
                                    $"Function: {fc.getName()} replaced with injection: {glb.pcodeinjectlib.getCallFixupName(fc.getInjectId())}");
                                deleteCallSpec(fc);
                            }
                        }
                        else if (inlineSubFunction(fc)) {
                            data.warningHeader("Inlined function: " + fc.getName());
                            deleteCallSpec(fc);
                        }
                    }
                }
            }
            injectlist.Clear();
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
        /// Individual PcodeOps from the Funcdata being in-lined are cloned into
        /// the Funcdata for \b this flow, preserving their original address.
        /// Any RETURN op is replaced with jump to first address following the call site.
        /// \param inlineflow is the given in-line flow to clone
        /// \param retaddr is the first address after the call site in \b this flow
        public void inlineClone(FlowInfo inlineflow, Address retaddr)
        {
            IEnumerator<PcodeOp> iter = inlineflow.data.beginOpDead();
            while (iter.MoveNext()) {
                PcodeOp op = iter.Current;
                PcodeOp cloneop;
                if ((op.code() == OpCode.CPUI_RETURN) && (!retaddr.isInvalid())) {
                    cloneop = data.newOp(1, op.getSeqNum());
                    data.opSetOpcode(cloneop, OpCode.CPUI_BRANCH);
                    Varnode vn = data.newCodeRef(retaddr);
                    data.opSetInput(cloneop, vn, 0);
                }
                else
                    cloneop = data.cloneOp(op, op.getSeqNum());
                if (cloneop.isCallOrBranch())
                    xrefInlinedBranch(cloneop);
            }
            // Copy in the cross-referencing
            unprocessed.AddRange(inlineflow.unprocessed);
            addrlist.AddRange(inlineflow.addrlist);
            foreach(KeyValuePair<Address, VisitStat> pair in inlineflow.visited) {
                visited.Add(pair.Key, pair.Value);
            }
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
            IEnumerator<PcodeOp> iter = inlineflow.data.beginOpDead();
            while (iter.MoveNext()) {
                PcodeOp op = iter.Current;
                if (op.code() == OpCode.CPUI_RETURN) break;
                SeqNum myseq = new SeqNum(calladdr, op.getSeqNum().getTime());
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
        public bool hasUnimplemented() => ((flags & FlowFlag.unimplemented_present)!= 0);

        /// Does \b this flow reach inaccessible data
        public bool hasBadData() => ((flags & FlowFlag.baddata_present)!= 0);

        /// Does \b this flow out of bound
        public bool hasOutOfBounds() => ((flags & FlowFlag.outofbounds_present)!= 0);

        /// Does \b this flow reinterpret bytes
        public bool hasReinterpreted() => ((flags & FlowFlag.reinterpreted_present)!= 0);

        /// Does \b this flow have too many instructions
        public bool hasTooManyInstructions() => ((flags & FlowFlag.toomanyinstructions_present)!= 0);

        /// Is \b this flow to be in-lined
        public bool isFlowForInline() => ((flags & FlowFlag.flow_forinline)!= 0);

        /// Should jump table structure be recorded
        public bool doesJumpRecord() => ((flags & FlowFlag.record_jumploads)!= 0);
    }
}
