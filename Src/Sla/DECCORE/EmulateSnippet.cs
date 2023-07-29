using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief Emulate a \e snippet of PcodeOps out of a functional context
    /// Emulation is performed on a short sequence (\b snippet) of PcodeOpRaw objects.
    /// Control-flow emulation is limited to this snippet; BRANCH and CBRANCH operations
    /// can happen using p-code relative branching.  Executing BRANCHIND, CALL, CALLIND,
    /// CALLOTHER, STORE, MULTIEQUAL, INDIRECT, SEGMENTOP, CPOOLOP, and NEW
    /// ops is treated as illegal and an exception is thrown.
    /// Expressions can only use temporary registers or read from the LoadImage.
    /// The set of PcodeOpRaw objects in the snippet is provided by emitting p-code to the object
    /// returned by buildEmitter().  This is designed for one-time initialization of this
    /// class, which can be repeatedly used by calling resetMemory() between executions.
    internal class EmulateSnippet : Emulate
    {
        /// The underlying Architecture for the program being emulated
        private Architecture glb;
        /// Sequence of p-code ops to be executed
        private List<PcodeOpRaw> opList;
        /// Varnodes allocated for ops
        private List<VarnodeData> varList;
        /// Values stored in temporary registers
        private Dictionary<uintb, uintb> tempValues;
        /// Current p-code op being executed
        private PcodeOpRaw currentOp;
        /// Index of current p-code op being executed
        private int pos;

        /// \brief Pull a value from the load-image given a specific address
        /// A contiguous chunk of memory is pulled from the load-image and returned as a
        /// constant value, respecting the endianess of the address space.
        /// \param spc is the address space to pull the value from
        /// \param offset is the starting address offset (from within the space) to pull the value from
        /// \param sz is the number of bytes to pull from memory
        /// \return indicated bytes arranged as a constant value
        private uintb getLoadImageValue(AddrSpace spc, uintb offset, int4 sz)
        {
            LoadImage* loadimage = glb.loader;
            uintb res;

            loadimage.loadFill((uint1*)&res, sizeof(uintb), Address(spc, off));

            if ((HOST_ENDIAN == 1) != spc.isBigEndian())
                res = byte_swap(res, sizeof(uintb));
            if (spc.isBigEndian() && (sz < sizeof(uintb)))
                res >>= (sizeof(uintb) - sz) * 8;
            else
                res &= calc_mask(sz);
            return res;
        }

        protected override void executeUnary()
        {
            uintb in1 = getVarnodeValue(currentOp.getInput(0));
            uintb out = currentBehave.evaluateUnary(currentOp.getOutput().size,
                                 currentOp.getInput(0).size, in1);
            setVarnodeValue(currentOp.getOutput().offset, out);
        }

        protected override void executeBinary()
        {
            uintb in1 = getVarnodeValue(currentOp.getInput(0));
            uintb in2 = getVarnodeValue(currentOp.getInput(1));
            uintb out = currentBehave.evaluateBinary(currentOp.getOutput().size,
                                  currentOp.getInput(0).size, in1, in2);
            setVarnodeValue(currentOp.getOutput().offset, out);
        }

        protected override void executeLoad()
        {
            // op will be null, use current_op
            uintb off = getVarnodeValue(currentOp.getInput(1));
            AddrSpace* spc = currentOp.getInput(0).getSpaceFromConst();
            off = AddrSpace::addressToByte(off, spc.getWordSize());
            int4 sz = currentOp.getOutput().size;
            uintb res = getLoadImageValue(spc, off, sz);
            setVarnodeValue(currentOp.getOutput().offset, res);
        }

        protected override void executeStore()
        {
            throw new LowlevelError("Illegal p-code operation in snippet: " + (string)get_opname(currentOp.getOpcode()));
        }

        protected override void executeBranch()
        {
            VarnodeData* vn = currentOp.getInput(0);
            if (vn.space.getType() != IPTR_CONSTANT)
                throw new LowlevelError("Tried to emulate absolute branch in snippet code");
            int4 rel = (int4)vn.offset;
            pos += rel;
            if ((pos < 0) || (pos > opList.size()))
                throw new LowlevelError("Relative branch out of bounds in snippet code");
            if (pos == opList.size())
            {
                emu_halted = true;
                return;
            }
            setCurrentOp(pos);
        }

        protected override bool executeCbranch()
        {
            // op will be null, use current_op
            uintb cond = getVarnodeValue(currentOp.getInput(1));
            // We must take into account the booleanflip bit with pcode from the syntax tree
            return (cond != 0);
        }

        protected override void executeBranchind()
        {
            throw new LowlevelError("Illegal p-code operation in snippet: " + (string)get_opname(currentOp.getOpcode()));
        }

        protected override void executeCall()
        {
            throw new LowlevelError("Illegal p-code operation in snippet: " + (string)get_opname(currentOp.getOpcode()));
        }

        protected override void executeCallind()
        {
            throw new LowlevelError("Illegal p-code operation in snippet: " + (string)get_opname(currentOp.getOpcode()));
        }

        protected override void executeCallother()
        {
            throw new LowlevelError("Illegal p-code operation in snippet: " + (string)get_opname(currentOp.getOpcode()));
        }

        protected override void executeMultiequal()
        {
            throw new LowlevelError("Illegal p-code operation in snippet: " + (string)get_opname(currentOp.getOpcode()));
        }

        protected override void executeIndirect()
        {
            throw new LowlevelError("Illegal p-code operation in snippet: " + (string)get_opname(currentOp.getOpcode()));
        }

        protected override void executeSegmentOp()
        {
            throw new LowlevelError("Illegal p-code operation in snippet: " + (string)get_opname(currentOp.getOpcode()));
        }

        protected override void executeCpoolRef()
        {
            throw new LowlevelError("Illegal p-code operation in snippet: " + (string)get_opname(currentOp.getOpcode()));
        }

        protected override void executeNew()
        {
            throw new LowlevelError("Illegal p-code operation in snippet: " + (string)get_opname(currentOp.getOpcode()));
        }

        protected override void fallthruOp()
        {
            pos += 1;
            if (pos == opList.size())
            {
                emu_halted = true;
                return;
            }
            setCurrentOp(pos);
        }

        public EmulateSnippet(Architecture g)
        {
            glb = g;
            pos = 0;
            currentOp = null;
        }
        
        ~EmulateSnippet()
        {
            for (int4 i = 0; i < opList.size(); ++i)
                delete opList[i];
            for (int4 i = 0; i < varList.size(); ++i)
                delete varList[i];
        }

        public override void setExecuteAddress(Address addr)
        {
            setCurrentOp(0);
        }

        public override Address getExecuteAddress() => currentOp.getAddr();

        /// Get the underlying Architecture
        public Architecture getArch() => glb;

        /// \brief Reset the emulation snippet
        /// Reset the memory state, and set the first p-code op as current.
        public void resetMemory()
        {
            tempValues.clear();
            setCurrentOp(0);
            emu_halted = false;
        }

        /// \brief Provide the caller with an emitter for building the p-code snippet
        ///
        /// Any p-code produced by the PcodeEmit, when triggered by the caller, becomes
        /// part of the \e snippet that will get emulated by \b this. The caller should
        /// free the PcodeEmit object immediately after use.
        /// \param inst is the \e opcode to \e behavior map the emitter will use
        /// \param uniqReserve is the starting offset within the \e unique address space for any temporary registers
        /// \return the newly constructed emitter
        public PcodeEmit buildEmitter(List<OpBehavior> inst, uintb uniqReserve)
        {
            return new PcodeEmitCache(opList, varList, inst, uniqReserve);
        }

        /// \brief Check for p-code that is deemed illegal for a \e snippet
        ///
        /// This method facilitates enforcement of the formal rules for snippet code.
        ///   - Branches must use p-code relative addressing.
        ///   - Snippets can only read/write from temporary registers
        ///   - Snippets cannot use BRANCHIND, CALL, CALLIND, CALLOTHER, STORE, SEGMENTOP, CPOOLREF,
        ///              NEW, MULTIEQUAL, or INDIRECT
        ///
        /// \return \b true if the current snippet is legal
        public bool checkForLegalCode()
        {
            for (int4 i = 0; i < opList.size(); ++i)
            {
                PcodeOpRaw* op = opList[i];
                VarnodeData* vn;
                OpCode opc = op.getOpcode();
                if (opc == CPUI_BRANCHIND || opc == CPUI_CALL || opc == CPUI_CALLIND || opc == CPUI_CALLOTHER ||
                opc == CPUI_STORE || opc == CPUI_SEGMENTOP || opc == CPUI_CPOOLREF ||
                opc == CPUI_NEW || opc == CPUI_MULTIEQUAL || opc == CPUI_INDIRECT)
                    return false;
                if (opc == CPUI_BRANCH)
                {
                    vn = op.getInput(0);
                    if (vn.space.getType() != IPTR_CONSTANT)  // Only relative branching allowed
                        return false;
                }
                vn = op.getOutput();
                if (vn != (VarnodeData*)0)
                {
                    if (vn.space.getType() != IPTR_INTERNAL)
                        return false;                   // Can only write to temporaries
                }
                for (int4 j = 0; j < op.numInput(); ++j)
                {
                    vn = op.getInput(j);
                    if (vn.space.getType() == IPTR_PROCESSOR)
                        return false;                   // Cannot read from normal registers
                }
            }
            return true;
        }

        /// \brief Set the current executing p-code op by index
        /// The i-th p-code op in the snippet sequence is set as the currently executing op.
        /// \param i is the index
        public void setCurrentOp(int i)
        {
            pos = i;
            currentOp = opList[i];
            currentBehave = currentOp.getBehavior();
        }

        /// \brief Set a temporary register value in the machine state
        /// The temporary Varnode's storage offset is used as key into the machine state map.
        /// \param offset is the temporary storage offset
        /// \param val is the value to put into the machine state
        public void setVarnodeValue(uintb offset, uintb val)
        {
            tempValues[offset] = val;
        }

        /// \brief Retrieve the value of a Varnode from the current machine state
        ///
        /// If the Varnode is a temporary registers, the storage offset is used to look up
        /// the value from the machine state cache. If the Varnode represents a RAM location,
        /// the value is pulled directly out of the load-image.
        /// If the value does not exist, a "Read before write" exception is thrown.
        /// \param vn is the Varnode to read
        /// \return the retrieved value
        public uintb getVarnodeValue(VarnodeData vn)
        {
            AddrSpace* spc = vn.space;
            if (spc.getType() == IPTR_CONSTANT)
                return vn.offset;
            if (spc.getType() == IPTR_INTERNAL)
            {
                map<uintb, uintb>::const_iterator iter;
                iter = tempValues.find(vn.offset);
                if (iter != tempValues.end())
                    return (*iter).second;  // We have seen this varnode before
                throw new LowlevelError("Read before write in snippet emulation");
            }

            return getLoadImageValue(vn.space, vn.offset, vn.size);
        }

        /// \brief Retrieve a temporary register value directly
        ///
        /// This allows the user to obtain the final value of the snippet calculation, without
        /// having to have the Varnode object in hand.
        /// \param offset is the offset of the temporary register to retrieve
        /// \return the calculated value or 0 if the register was never written
        public uintb getTempValue(uintb offset)
        {
            map<uintb, uintb>::const_iterator iter = tempValues.find(offset);
            if (iter == tempValues.end())
                return 0;
            return (*iter).second;
        }
    }
}
