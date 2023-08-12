using Sla.CORE;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief A pcode-based emulator interface.
    ///
    /// The interface expects that the underlying emulation engine operates on individual pcode
    /// operations as its atomic operation.  The interface allows execution stepping through
    /// individual pcode operations. The interface allows
    /// querying of the \e current pcode op, the current machine address, and the rest of the
    /// machine state.
    internal abstract class Emulate
    {
        /// Set to \b true if the emulator is halted
        protected bool emu_halted;
        /// Behavior of the next op to execute
        protected OpBehavior? currentBehave;

        /// Execute a unary arithmetic/logical operation
        protected abstract void executeUnary();

        /// Execute a binary arithmetic/logical operation
        protected abstract void executeBinary();

        /// Standard behavior for a p-code LOAD
        protected abstract void executeLoad();

        /// Standard behavior for a p-code STORE
        protected abstract void executeStore();

        /// \brief Standard behavior for a BRANCH
        /// This routine performs a standard p-code BRANCH operation on the memory state.
        /// This same routine is used for CBRANCH operations if the condition
        /// has evaluated to \b true.
        protected abstract void executeBranch();

        /// \brief Check if the conditional of a CBRANCH is \b true
        /// This routine only checks if the condition for a p-code CBRANCH is true.
        /// It does \e not perform the actual branch.
        /// \return the boolean state indicated by the condition
        protected abstract bool executeCbranch();

        /// Standard behavior for a BRANCHIND
        protected abstract void executeBranchind();

        /// Standard behavior for a p-code CALL
        protected abstract void executeCall();

        /// Standard behavior for a CALLIND
        protected abstract void executeCallind();

        /// Standard behavior for a user-defined p-code op
        protected abstract void executeCallother();

        /// Standard behavior for a MULTIEQUAL (phi-node)
        protected abstract void executeMultiequal();

        /// Standard behavior for an INDIRECT op
        protected abstract void executeIndirect();

        /// Behavior for a SEGMENTOP
        protected abstract void executeSegmentOp();

        /// Standard behavior for a CPOOLREF (constant pool reference) op
        protected abstract void executeCpoolRef();

        /// Standard behavior for (low-level) NEW op
        protected abstract void executeNew();

        /// Standard p-code fall-thru semantics
        protected abstract void fallthruOp();

        public Emulate()
        {
            emu_halted = true;
            currentBehave = null;
        }  ///< generic emulator constructor
        
        ~Emulate()
        {
        }

        /// Set the \e halt state of the emulator
        /// Applications and breakpoints can use this method and its companion getHalt() to
        /// terminate and restart the main emulator loop as needed. The emulator itself makes no use
        /// of this routine or the associated state variable \b emu_halted.
        /// \param val is what the halt state of the emulator should be set to
        public void setHalt(bool val)
        {
            emu_halted = val;
        }

        /// Get the \e halt state of the emulator
        /// Applications and breakpoints can use this method and its companion setHalt() to
        /// terminate and restart the main emulator loop as needed.  The emulator itself makes no use
        /// of this routine or the associated state variable \b emu_halted.
        /// \return \b true if the emulator is in a "halted" state.
        public bool getHalt()
        {
            return emu_halted;
        }

        /// Set the address of the next instruction to emulate
        public abstract void setExecuteAddress(Address addr);

        /// Get the address of the current instruction being executed
        public abstract Address getExecuteAddress();

        /// Do a single pcode op step
        /// This method executes a single pcode operation, the current one (returned by getCurrentOp()).
        /// The MemoryState of the emulator is queried and changed as needed to accomplish this.
        public void executeCurrentOp()
        {
            if (currentBehave == (OpBehavior)null) {   // Presumably a NO-OP
                fallthruOp();
                return;
            }
            if (currentBehave.isSpecial()) {
                switch (currentBehave.getOpcode()) {
                    case OpCode.CPUI_LOAD:
                        executeLoad();
                        fallthruOp();
                        break;
                    case OpCode.CPUI_STORE:
                        executeStore();
                        fallthruOp();
                        break;
                    case OpCode.CPUI_BRANCH:
                        executeBranch();
                        break;
                    case OpCode.CPUI_CBRANCH:
                        if (executeCbranch())
                            executeBranch();
                        else
                            fallthruOp();
                        break;
                    case OpCode.CPUI_BRANCHIND:
                        executeBranchind();
                        break;
                    case OpCode.CPUI_CALL:
                        executeCall();
                        break;
                    case OpCode.CPUI_CALLIND:
                        executeCallind();
                        break;
                    case OpCode.CPUI_CALLOTHER:
                        executeCallother();
                        break;
                    case OpCode.CPUI_RETURN:
                        executeBranchind();
                        break;
                    case OpCode.CPUI_MULTIEQUAL:
                        executeMultiequal();
                        fallthruOp();
                        break;
                    case OpCode.CPUI_INDIRECT:
                        executeIndirect();
                        fallthruOp();
                        break;
                    case OpCode.CPUI_SEGMENTOP:
                        executeSegmentOp();
                        fallthruOp();
                        break;
                    case OpCode.CPUI_CPOOLREF:
                        executeCpoolRef();
                        fallthruOp();
                        break;
                    case OpCode.CPUI_NEW:
                        executeNew();
                        fallthruOp();
                        break;
                    default:
                        throw new LowlevelError("Bad special op");
                }
            }
            else if (currentBehave.isUnary()) {
                // Unary operation
                executeUnary();
                fallthruOp();
            }
            else {
                // Binary operation
                executeBinary();
                fallthruOp();       // All binary ops are fallthrus
            }
        }
    }
}
