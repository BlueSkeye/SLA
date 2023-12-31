﻿using Sla.CORE;

namespace Sla.DECCORE
{
    /// \brief An abstract Emulate class using a MemoryState object as the backing machine state
    /// Most p-code operations are implemented using the MemoryState to fetch and store
    /// values.  Control-flow is implemented partially in that setExecuteAddress() is called
    /// to indicate which instruction is being executed. The derived class must provide
    ///   - fallthruOp()
    ///   - setExecuteAddress()
    ///   - getExecuteAddress()
    /// The following p-code operations are stubbed out and will throw an exception:
    /// CALLOTHER, MULTIEQUAL, INDIRECT, CPOOLREF, SEGMENTOP, and NEW.
    /// Of course the derived class can override these.
    internal abstract class EmulateMemory : Emulate
    {
        /// The memory state of the emulator
        protected MemoryState memstate;
        /// Current op to execute
        protected PcodeOpRaw? currentOp;

        protected override void executeUnary()
        {
            ulong in1 = memstate.getValue(currentOp.getInput(0));
            ulong @out = currentBehave.evaluateUnary((int)currentOp.getOutput().size,
                (int)currentOp.getInput(0).size, in1);
            memstate.setValue(currentOp.getOutput(), @out);
        }

        protected override void executeBinary()
        {
            ulong in1 = memstate.getValue(currentOp.getInput(0));
            ulong in2 = memstate.getValue(currentOp.getInput(1));
            ulong @out = currentBehave.evaluateBinary((int)currentOp.getOutput().size,
                (int)currentOp.getInput(0).size, in1, in2);
            memstate.setValue(currentOp.getOutput(), @out);
        }

        protected override void executeLoad()
        {
            ulong off = memstate.getValue(currentOp.getInput(1));
            AddrSpace spc = currentOp.getInput(0).getSpaceFromConst();

            off = AddrSpace.addressToByte(off, spc.getWordSize());
            ulong res = memstate.getValue(spc, off, (int)currentOp.getOutput().size);
            memstate.setValue(currentOp.getOutput(), res);
        }

        protected override void executeStore()
        {
            // Value being stored
            ulong val = memstate.getValue(currentOp.getInput(2));
            // Offset to store at
            ulong off = memstate.getValue(currentOp.getInput(1));
            // Space to store in
            AddrSpace spc = currentOp.getInput(0).getSpaceFromConst();

            off = AddrSpace.addressToByte(off, spc.getWordSize());
            memstate.setValue(spc, off, (int)currentOp.getInput(2).size, val);
        }

        protected override void executeBranch()
        {
            setExecuteAddress(currentOp.getInput(0).getAddr());
        }

        protected override bool executeCbranch()
        {
            ulong cond = memstate.getValue(currentOp.getInput(1));
            return (cond != 0);
        }

        protected override void executeBranchind()
        {
            ulong off = memstate.getValue(currentOp.getInput(0));
            setExecuteAddress(new Address(currentOp.getAddr().getSpace(), off));
        }

        protected override void executeCall()
        {
            setExecuteAddress(currentOp.getInput(0).getAddr());
        }

        protected override void executeCallind()
        {
            ulong off = memstate.getValue(currentOp.getInput(0));
            setExecuteAddress(new Address(currentOp.getAddr().getSpace(), off));
        }

        protected override void executeCallother()
        {
            throw new LowlevelError("CALLOTHER emulation not currently supported");
        }

        protected override void executeMultiequal()
        {
            throw new LowlevelError("MULTIEQUAL appearing in unheritaged code?");
        }

        protected override void executeIndirect()
        {
            throw new LowlevelError("INDIRECT appearing in unheritaged code?");
        }

        protected override void executeSegmentOp()
        {
            throw new LowlevelError("SEGMENTOP emulation not currently supported");
        }

        protected override void executeCpoolRef()
        {
            throw new LowlevelError("Cannot currently emulate cpool operator");
        }

        protected override void executeNew()
        {
            throw new LowlevelError("Cannot currently emulate new operator");
        }

        /// Construct given a memory state
        public EmulateMemory(MemoryState mem)
        {
            memstate = mem;
            currentOp = null;
        }

        /// Get the emulator's memory state
        /// \return the memory state object which this emulator uses
        public MemoryState getMemoryState() => memstate;
    }
}
