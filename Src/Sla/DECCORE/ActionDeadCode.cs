﻿using Sla.CORE;

namespace Sla.DECCORE
{
    /// \brief Dead code removal.  Eliminate \e dead p-code ops
    ///
    /// This is a very fine grained algorithm, it detects usage
    /// of individual bits within the Varnode, not just use of the
    /// Varnode itself.  Each Varnode has a \e consumed word, which
    /// indicates if a bit in the Varnode is being used, and it has
    /// two flags layed out as follows:
    ///    - Varnode::lisconsume = varnode is in the working list
    ///    - Varnode::vacconsume = vacuously used bit
    ///            there is a path from the varnode through assignment
    ///            op outputs down to a varnode that is used
    ///
    /// The algorithm works by back propagating the \e consumed value
    /// up from the output of the op to its inputs, starting with
    /// a set of seed Varnodes which are marked as completely used
    /// (function inputs, branch conditions, ...) For each propagation
    /// the particular op being passed through can transform the
    /// "bit usage" List of the output to obtain the input.
    internal class ActionDeadCode : Action
    {
        /// Given a new \e consume value to push to a Varnode, determine if this changes
        /// the Varnodes consume value and whether to push the Varnode onto the work-list.
        /// \param val is the new consume value
        /// \param vn is the Varnode to push to
        /// \param worklist is the current work-list
        private static void pushConsumed(ulong val, Varnode vn, List<Varnode> worklist)
        {
            ulong newval = (val | vn.getConsume()) & Globals.calc_mask((uint)vn.getSize());
            if ((newval == vn.getConsume()) && vn.isConsumeVacuous()) return;
            vn.setConsumeVacuous();
            if (!vn.isConsumeList())
            { // Check if already in list
                vn.setConsumeList();   // Mark as in the list
                if (vn.isWritten())
                    worklist.Add(vn); // add to list
            }
            vn.setConsume(newval);
        }

        /// \brief Propagate the \e consumed value for one Varnode
        ///
        /// The Varnode at the top of the stack is popped off, and its current
        /// \e consumed value is propagated  backward to the inputs of the op
        /// that produced it.
        /// \param worklist is the current stack of dirty Varnodes
        private static void propagateConsumed(List<Varnode> worklist)
        {
            Varnode vn = worklist.GetLastItem();
            worklist.RemoveLastItem();
            ulong outc = vn.getConsume();
            vn.clearConsumeList();

            PcodeOp op = vn.getDef() ?? throw new ApplicationException(); // Assume vn is written

            int sz;
            ulong a, b;

            switch (op.code()) {
                case OpCode.CPUI_INT_MULT:
                    b = Globals.coveringmask(outc);
                    if (op.getIn(1).isConstant()) {
                        int leastSet = Globals.leastsigbit_set(op.getIn(1).getOffset());
                        if (leastSet >= 0) {
                            a = Globals.calc_mask((uint)vn.getSize()) >> leastSet;
                            a &= b;
                        }
                        else
                            a = 0;
                    }
                    else
                        a = b;
                    pushConsumed(a, op.getIn(0), worklist);
                    pushConsumed(b, op.getIn(1), worklist);
                    break;
                case OpCode.CPUI_INT_ADD:
                case OpCode.CPUI_INT_SUB:
                    // Make sure value is filled out as a contiguous mask
                    a = Globals.coveringmask(outc);
                    pushConsumed(a, op.getIn(0), worklist);
                    pushConsumed(a, op.getIn(1), worklist);
                    break;
                case OpCode.CPUI_SUBPIECE:
                    sz = (int)op.getIn(1).getOffset();
                    if (sz >= sizeof(ulong))
                        // If we are truncating beyond the precision of the consume field
                        // this tells us nothing about consuming bits within the field
                        a = 0;
                    else
                        a = outc << (sz * 8);
                    if ((a == 0) && (outc != 0) && (op.getIn(0).getSize() > sizeof(ulong))) {
                        // If the consumed mask is zero because
                        // it isn't big enough to cover the whole varnode and
                        // there are still upper bits that are consumed
                        a = ulong.MaxValue;
                        // Set the highest bit possible in the mask to indicate some consumption
                        a = a ^ (a >> 1);
                    }
                    b = (outc == 0) ? 0 : ulong.MaxValue;
                    pushConsumed(a, op.getIn(0), worklist);
                    pushConsumed(b, op.getIn(1), worklist);
                    break;
                case OpCode.CPUI_PIECE:
                    sz = op.getIn(1).getSize();
                    if (vn.getSize() > sizeof(ulong)) {
                        // If the concatenation goes beyond the consume precision
                        if (sz >= sizeof(ulong)) {
                            // Assume the bits not in the consume field are consumed
                            a = ulong.MaxValue;
                            b = outc;
                        }
                        else {
                            a = (outc >> (sz * 8)) ^ ((ulong.MaxValue) << 8 * (sizeof(ulong) - sz));
                            b = outc ^ (a << (sz * 8));
                        }
                    }
                    else {
                        a = outc >> (sz * 8);
                        b = outc ^ (a << (sz * 8));
                    }
                    pushConsumed(a, op.getIn(0), worklist);
                    pushConsumed(b, op.getIn(1), worklist);
                    break;
                case OpCode.CPUI_INDIRECT:
                    pushConsumed(outc, op.getIn(0), worklist);
                    if (op.getIn(1).getSpace().getType() == spacetype.IPTR_IOP) {
                        PcodeOp indop = PcodeOp.getOpFromConst(op.getIn(1).getAddr());
                        if (!indop.isDead()) {
                            if (indop.code() == OpCode.CPUI_COPY) {
                                if (indop.getOut().characterizeOverlap(op.getOut()) > 0) {
                                    // Mark the copy as consumed
                                    pushConsumed(ulong.MaxValue, indop.getOut(), worklist);
                                    indop.setIndirectSource();
                                }
                                // If we reach here, there isn't a true block of INDIRECT (RuleIndirectCollapse will convert it to COPY)
                            }
                            else
                                indop.setIndirectSource();
                        }
                    }
                    break;
                case OpCode.CPUI_COPY:
                case OpCode.CPUI_INT_NEGATE:
                    pushConsumed(outc, op.getIn(0), worklist);
                    break;
                case OpCode.CPUI_INT_XOR:
                case OpCode.CPUI_INT_OR:
                    pushConsumed(outc, op.getIn(0), worklist);
                    pushConsumed(outc, op.getIn(1), worklist);
                    break;
                case OpCode.CPUI_INT_AND:
                    if (op.getIn(1).isConstant()) {
                        ulong val = op.getIn(1).getOffset();
                        pushConsumed(outc & val, op.getIn(0), worklist);
                        pushConsumed(outc, op.getIn(1), worklist);
                    }
                    else {
                        pushConsumed(outc, op.getIn(0), worklist);
                        pushConsumed(outc, op.getIn(1), worklist);
                    }
                    break;
                case OpCode.CPUI_MULTIEQUAL:
                    for (int i = 0; i < op.numInput(); ++i)
                        pushConsumed(outc, op.getIn(i), worklist);
                    break;
                case OpCode.CPUI_INT_ZEXT:
                    pushConsumed(outc, op.getIn(0), worklist);
                    break;
                case OpCode.CPUI_INT_SEXT:
                    b = Globals.calc_mask((uint)op.getIn(0).getSize());
                    a = outc & b;
                    if (outc > b)
                        a |= (b ^ (b >> 1));    // Make sure signbit is marked used
                    pushConsumed(a, op.getIn(0), worklist);
                    break;
                case OpCode.CPUI_INT_LEFT:
                    if (op.getIn(1).isConstant()) {
                        sz = vn.getSize();
                        int sa = (int)op.getIn(1).getOffset();
                        if (sz > sizeof(ulong)) {
                            // If there exists bits beyond the precision of the consume field
                            if (sa >= 8 * sizeof(ulong))
                                // Make sure we assume one bits where we shift in unrepresented bits
                                a = ulong.MaxValue;
                            else
                                a = (outc >> sa) ^ ((ulong.MaxValue) << (8 * sizeof(ulong) - sa));
                            sz = 8 * sz - sa;
                            if (sz < 8 * sizeof(ulong)) {
                                ulong mask = ulong.MaxValue;
                                mask <<= sz;
                                // Make sure high bits that are left shifted out are not marked consumed
                                a = a & ~mask;
                            }
                        }
                        else
                            // Most cases just do this
                            a = outc >> sa;
                        b = (outc == 0) ? 0 : ulong.MaxValue;
                        pushConsumed(a, op.getIn(0), worklist);
                        pushConsumed(b, op.getIn(1), worklist);
                    }
                    else {
                        a = (outc == 0) ? 0 : ulong.MaxValue;
                        pushConsumed(a, op.getIn(0), worklist);
                        pushConsumed(a, op.getIn(1), worklist);
                    }
                    break;
                case OpCode.CPUI_INT_RIGHT:
                    if (op.getIn(1).isConstant()) {
                        int sa = (int)op.getIn(1).getOffset();
                        if (sa >= 8 * sizeof(ulong))
                            // If the shift is beyond the precision of the consume field
                            // We know nothing about the low order consumption of the input bits
                            a = 0;
                        else
                            // Most cases just do this
                            a = outc << sa;
                        b = (outc == 0) ? 0 : ulong.MaxValue;
                        pushConsumed(a, op.getIn(0), worklist);
                        pushConsumed(b, op.getIn(1), worklist);
                    }
                    else {
                        a = (outc == 0) ? 0 : ulong.MaxValue;
                        pushConsumed(a, op.getIn(0), worklist);
                        pushConsumed(a, op.getIn(1), worklist);
                    }
                    break;
                case OpCode.CPUI_INT_LESS:
                case OpCode.CPUI_INT_LESSEQUAL:
                case OpCode.CPUI_INT_EQUAL:
                case OpCode.CPUI_INT_NOTEQUAL:
                    if (outc == 0)
                        a = 0;
                    else
                        // Anywhere we know is zero, is not getting "consumed"
                        a = op.getIn(0).getNZMask() | op.getIn(1).getNZMask();
                    pushConsumed(a, op.getIn(0), worklist);
                    pushConsumed(a, op.getIn(1), worklist);
                    break;
                case OpCode.CPUI_INSERT:
                    a = 1;
                    a <<= (int)op.getIn(3).getOffset();
                    a -= 1; // Insert mask
                    pushConsumed(a, op.getIn(1), worklist);
                    a <<= (int)op.getIn(2).getOffset();
                    pushConsumed(outc & ~a, op.getIn(0), worklist);
                    b = (outc == 0) ? 0 : ulong.MaxValue;
                    pushConsumed(b, op.getIn(2), worklist);
                    pushConsumed(b, op.getIn(3), worklist);
                    break;
                case OpCode.CPUI_EXTRACT:
                    a = 1;
                    a <<= (int)op.getIn(2).getOffset();
                    a -= 1; // Extract mask
                    a &= outc;  // Consumed bits of mask
                    a <<= (int)op.getIn(1).getOffset();
                    pushConsumed(a, op.getIn(0), worklist);
                    b = (outc == 0) ? 0 : ulong.MaxValue;
                    pushConsumed(b, op.getIn(1), worklist);
                    pushConsumed(b, op.getIn(2), worklist);
                    break;
                case OpCode.CPUI_POPCOUNT:
                case OpCode.CPUI_LZCOUNT:
                    // Mask for possible bits that could be set
                    a = (uint)(16 * op.getIn(0).getSize() - 1);
                    // Of the bits that could be set, which are consumed
                    a &= outc;
                    // if any consumed, treat all input bits as consumed
                    b = (a == 0) ? 0 : ulong.MaxValue;
                    pushConsumed(b, op.getIn(0), worklist);
                    break;
                case OpCode.CPUI_CALL:
                case OpCode.CPUI_CALLIND:
                    // Call output doesn't indicate consumption of inputs
                    break;
                default:
                    // all or nothing
                    a = (outc == 0) ? 0 : ulong.MaxValue;
                    for (int i = 0; i < op.numInput(); ++i)
                        pushConsumed(a, op.getIn(i), worklist);
                    break;
            }
        }

        /// \brief Deal with unconsumed Varnodes
        ///
        /// For a Varnode, none of whose bits are consumed, eliminate the PcodeOp defining it
        /// and replace Varnode inputs to ops that officially read it with zero constants.
        /// \param vn is the Varnode
        /// \param data is the function being analyzed
        /// \return true if the Varnode was eliminated
        private static bool neverConsumed(Varnode vn, Funcdata data)
        {
            // Not enough precision to really tell
            if (vn.getSize() > sizeof(ulong)) return false;
            PcodeOp op;
            IEnumerator<PcodeOp> iter = vn.beginDescend();
            while (iter.MoveNext()) {
                // Advance before ref is removed
                op = iter.Current;
                int slot = op.getSlot(vn);
                // Replace vn with 0 whereever it is read
                // We don't worry about putting a constant in a marker
                // because if vn is not consumed and is input to a marker
                // then the output is also not consumed and the marker
                // op is about to be deleted anyway
                data.opSetInput(op, data.newConstant(vn.getSize(), 0), slot);
            }
            op = vn.getDef();
            if (op.isCall())
                data.opUnsetOutput(op); // For calls just get rid of output
            else
                data.opDestroy(op); // Otherwise completely remove the op
            return true;
        }

        /// \brief Determine how the given sub-function parameters are consumed
        ///
        /// Set the consume property for each input Varnode of a OpCode.CPUI_CALL or OpCode.CPUI_CALLIND.
        /// If the prototype is locked, assume parameters are entirely consumed.
        /// \param fc is the call specification for the given sub-function
        /// \param worklist will hold input Varnodes that can propagate their consume property
        private static void markConsumedParameters(FuncCallSpecs fc, List<Varnode> worklist)
        {
            PcodeOp callOp = fc.getOp();
            pushConsumed(ulong.MaxValue, callOp.getIn(0), worklist);      // In all cases the first operand is fully consumed
            if (fc.isInputLocked() || fc.isInputActive()) {
                // If the prototype is locked in, or in active recovery
                for (int i = 1; i < callOp.numInput(); ++i)
                    pushConsumed(ulong.MaxValue, callOp.getIn(i), worklist);  // Treat all parameters as fully consumed
                return;
            }
            for (int i = 1; i < callOp.numInput(); ++i) {
                Varnode vn = callOp.getIn(i);
                ulong consumeVal;
                if (vn.isAutoLive())
                    consumeVal = ulong.MaxValue;
                else
                    consumeVal = Globals.minimalmask(vn.getNZMask());
                int bytesConsumed = fc.getInputBytesConsumed(i);
                if (bytesConsumed != 0)
                    consumeVal &= Globals.calc_mask((uint)bytesConsumed);
                pushConsumed(consumeVal, vn, worklist);
            }
        }

        /// \brief Determine how the \e return \e values for the given function are consumed
        ///
        /// Examine each OpCode.CPUI_RETURN to see how the Varnode input is consumed.
        /// If the function's prototype is locked, assume the Varnode is entirely consumed.
        /// If there are no OpCode.CPUI_RETURN ops, return 0
        /// \param data is the given function
        /// \return the bit mask of what is consumed
        private static ulong gatherConsumedReturn(Funcdata data)
        {
            if (data.getFuncProto().isOutputLocked() || data.getActiveOutput() != (ParamActive)null)
                return ulong.MaxValue;
            IEnumerator<PcodeOp> iter = data.beginOp(OpCode.CPUI_RETURN);
            ulong consumeVal = 0;
            while (iter.MoveNext()) {
                PcodeOp returnOp = iter.Current;
                if (returnOp.isDead()) continue;
                if (returnOp.numInput() > 1) {
                    Varnode vn = returnOp.getIn(1);
                    consumeVal |= Globals.minimalmask(vn.getNZMask());
                }
            }
            int val = data.getFuncProto().getReturnBytesConsumed();
            if (val != 0) {
                consumeVal &= Globals.calc_mask((uint)val);
            }
            return consumeVal;
        }

        /// \brief Determine if the given Varnode may eventually collapse to a constant
        ///
        /// Recursively check if the Varnode is either:
        ///   - Copied from a constant
        ///   - The result of adding constants
        ///   - Loaded from a pointer that is a constant
        ///
        /// \param vn is the given Varnode
        /// \param addCount is the number of OpCode.CPUI_INT_ADD operations seen so far
        /// \param loadCount is the number of OpCode.CPUI_LOAD operations seen so far
        /// \return \b true if the Varnode (might) collapse to a constant
        private static bool isEventualConstant(Varnode vn, int addCount, int loadCount)
        {
            if (vn.isConstant()) return true;
            if (!vn.isWritten()) return false;
            PcodeOp op = vn.getDef();
            while (op.code() == OpCode.CPUI_COPY) {
                vn = op.getIn(0);
                if (vn.isConstant()) return true;
                if (!vn.isWritten()) return false;
                op = vn.getDef();
            }
            switch (op.code()) {
                case OpCode.CPUI_INT_ADD:
                    if (addCount > 0) return false;
                    if (!isEventualConstant(op.getIn(0), addCount + 1, loadCount))
                        return false;
                    return isEventualConstant(op.getIn(1), addCount + 1, loadCount);
                case OpCode.CPUI_LOAD:
                    if (loadCount > 0) return false;
                    return isEventualConstant(op.getIn(1), 0, loadCount + 1);
                case OpCode.CPUI_INT_LEFT:
                case OpCode.CPUI_INT_RIGHT:
                case OpCode.CPUI_INT_SRIGHT:
                case OpCode.CPUI_INT_MULT:
                    if (!op.getIn(1).isConstant())
                        return false;
                    return isEventualConstant(op.getIn(0), addCount, loadCount);
                case OpCode.CPUI_INT_ZEXT:
                case OpCode.CPUI_INT_SEXT:
                    return isEventualConstant(op.getIn(0), addCount, loadCount);
                default:
                    break;
            }
            return false;
        }

        /// \brief Check if there are any unconsumed LOADs that may be from volatile addresses.
        ///
        /// It may be too early to remove certain LOAD operations even though their result isn't
        /// consumed because it may be of a volatile address with side effects.  If a LOAD meets this
        /// criteria, it is added to the worklist and \b true is returned.
        /// \param data is the function being analyzed
        /// \param worklist is the container of consumed Varnodes to further process
        /// \return \b true if there was at least one LOAD added to the worklist
        private static bool lastChanceLoad(Funcdata data, List<Varnode> worklist)
        {
            if (data.getHeritagePass() > 1) return false;
            if (data.isJumptableRecoveryOn()) return false;
            IEnumerator<PcodeOp> iter = data.beginOp(OpCode.CPUI_LOAD);
            bool res = false;
            while (iter.MoveNext()) {
                PcodeOp op = iter.Current;
                if (op.isDead()) continue;
                Varnode vn = op.getOut();
                if (vn.isConsumeVacuous()) continue;
                if (isEventualConstant(op.getIn(1), 0, 0)) {
                    pushConsumed(ulong.MaxValue, vn, worklist);
                    vn.setAutoLiveHold();
                    res = true;
                }
            }
            return res;
        }

        public ActionDeadCode(string g)
            : base(0,"deadcode", g)
        {
        }

        public override Action? clone(ActionGroupList grouplist)
        {
            return (!grouplist.contains(getGroup())) ? null : new ActionDeadCode(getGroup());
        }

        public override int apply(Funcdata data)
        {
            int i;
            PcodeOp op;
            Varnode vn;
            ulong returnConsume;
            AddrSpaceManager manage = data.getArch();
            AddrSpace spc;

            // Clear consume flags
            IEnumerator<Varnode> viter = data.beginLoc();
            while (viter.MoveNext()) {
                vn = viter.Current;
                vn.clearConsumeList();
                vn.clearConsumeVacuous();
                vn.setConsume(0);
                if (vn.isAddrForce() && !vn.isDirectWrite())
                    vn.clearAddrForce();
            }

            // Set pre-live registers
            List<Varnode> worklist = new List<Varnode>();
            for (i = 0; i < manage.numSpaces(); ++i) {
                spc = manage.getSpace(i);
                if (spc == (AddrSpace)null || !spc.doesDeadcode()) continue;
                if (data.deadRemovalAllowed(spc)) continue; // Mark consumed if we have NOT heritaged
                viter = data.beginLoc(spc);
                // endviter = data.endLoc(spc);
                while (viter.MoveNext()) {
                    vn = viter.Current;
                    pushConsumed(ulong.MaxValue, vn, worklist);
                }
            }

            returnConsume = gatherConsumedReturn(data);
            IEnumerator<PcodeOp> iter = data.beginOpAlive();
            while (iter.MoveNext()) {
                op = iter.Current;
                op.clearIndirectSource();
                if (op.isCall()) {
                    // Postpone setting consumption on CALL and CALLIND inputs
                    if (op.isCallWithoutSpec()) {
                        for (i = 0; i < op.numInput(); ++i)
                            pushConsumed(ulong.MaxValue, op.getIn(i), worklist);
                    }
                    if (!op.isAssignment())
                        continue;
                    if (op.holdOutput())
                        pushConsumed(ulong.MaxValue, op.getOut(), worklist);
                }
                else if (!op.isAssignment())
                {
                    OpCode opc = op.code();
                    if (opc == OpCode.CPUI_RETURN) {
                        pushConsumed(ulong.MaxValue, op.getIn(0), worklist);
                        for (i = 1; i < op.numInput(); ++i)
                            pushConsumed(returnConsume, op.getIn(i), worklist);
                    }
                    else if (opc == OpCode.CPUI_BRANCHIND) {
                        JumpTable? jt = data.findJumpTable(op);
                        ulong mask;
                        if (jt != (JumpTable)null)
                            mask = jt.getSwitchVarConsume();
                        else
                            mask = ulong.MaxValue;
                        pushConsumed(mask, op.getIn(0), worklist);
                    }
                    else {
                        for (i = 0; i < op.numInput(); ++i)
                            pushConsumed(ulong.MaxValue, op.getIn(i), worklist);
                    }
                    // Postpone setting consumption on RETURN input
                    continue;
                }
                else {
                    for (i = 0; i < op.numInput(); ++i) {
                        vn = op.getIn(i);
                        if (vn.isAutoLive())
                            pushConsumed(ulong.MaxValue, vn, worklist);
                    }
                }
                vn = op.getOut() ?? throw new ApplicationException();
                if (vn.isAutoLive())
                    pushConsumed(ulong.MaxValue, vn, worklist);
            }

            // Mark consumption of call parameters
            for (i = 0; i < data.numCalls(); ++i)
                markConsumedParameters(data.getCallSpecs(i), worklist);

            // Propagate the consume flags
            while (!worklist.empty())
                propagateConsumed(worklist);

            if (lastChanceLoad(data, worklist)) {
                while (!worklist.empty())
                    propagateConsumed(worklist);
            }

            for (i = 0; i < manage.numSpaces(); ++i) {
                spc = manage.getSpace(i);
                if (spc == (AddrSpace)null || !spc.doesDeadcode()) continue;
                // Don't eliminate if we haven't heritaged
                if (!data.deadRemovalAllowed(spc)) continue;
                viter = data.beginLoc(spc);
                // endviter = data.endLoc(spc);
                int changecount = 0;
                while (viter.MoveNext()) {
                    vn = viter.Current;      // Advance iterator BEFORE (possibly) deleting varnode
                    if (!vn.isWritten()) continue;
                    bool vacflag = vn.isConsumeVacuous();
                    vn.clearConsumeList();
                    vn.clearConsumeVacuous();
                    if (!vacflag) {
                        // Not even vacuously consumed
                        op = vn.getDef();
                        changecount += 1;
                        if (op.isCall())
                            data.opUnsetOutput(op); // For calls just get rid of output
                        else
                            data.opDestroy(op); // Otherwise completely remove the op
                    }
                    else {
                        // Check for values that are never used, but bang around
                        // for a while
                        if (vn.getConsume() == 0) {
                            if (neverConsumed(vn, data))
                                changecount += 1;
                        }
                    }
                }
                if (changecount != 0)
                    // Record that we have seen dead code for this space
                    data.seenDeadcode(spc);
            }
#if OPACTION_DEBUG
            data.debugModPrint(getName()); // Print dead ops before freeing them
#endif
            data.clearDeadVarnodes();
            data.clearDeadOps();
            return 0;
        }
    }
}
