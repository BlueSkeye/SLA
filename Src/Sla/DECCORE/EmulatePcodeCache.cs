using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief A SLEIGH based implementation of the Emulate interface
    ///
    /// This implementation uses a Translate object to translate machine instructions into
    /// pcode and caches pcode ops for later use by the emulator.  The pcode is cached as soon
    /// as the execution address is set, either explicitly, or via branches and fallthrus.  There
    /// are additional methods for inspecting the pcode ops in the current instruction as a sequence.
    internal class EmulatePcodeCache : EmulateMemory
    {
        /// The SLEIGH translator
        private Translate trans;
        /// The cache of current p-code ops
        private List<PcodeOpRaw> opcache;
        /// The cache of current varnodes
        private List<VarnodeData> varcache;
        /// Map from OpCode to OpBehavior
        private List<OpBehavior> inst;
        /// The table of breakpoints
        private BreakTable breaktable;
        /// Address of current instruction being executed
        private Address current_address;
        /// \b true if next pcode op is start of instruction
        private bool instruction_start;
        /// Index of current pcode op within machine instruction
        private int current_op;
        /// Length of current instruction in bytes
        private int instruction_length;

        /// Clear the p-code cache
        /// Free all the VarnodeData and PcodeOpRaw objects and clear the cache
        private void clearCache()
        {
            for (int4 i = 0; i < opcache.size(); ++i)
                delete opcache[i];
            for (int4 i = 0; i < varcache.size(); ++i)
                delete varcache[i];
            opcache.clear();
            varcache.clear();
        }

        /// Cache pcode for instruction at given address
        /// This is a private routine which does the work of translating a machine instruction
        /// into pcode, putting it into the cache, and setting up the iterators
        /// \param addr is the address of the instruction to translate
        private void createInstruction(Address addr)
        {
            clearCache();
            PcodeEmitCache emit(opcache, varcache, inst,0);
            instruction_length = trans->oneInstruction(emit, addr);
            current_op = 0;
            instruction_start = true;
        }

        /// Set-up currentOp and currentBehave
        private void establishOp()
        {
            if (current_op < opcache.size())
            {
                currentOp = opcache[current_op];
                currentBehave = currentOp->getBehavior();
                return;
            }
            currentOp = (PcodeOpRaw*)0;
            currentBehave = (OpBehavior*)0;
        }

        /// Execute fallthru semantics for the pcode cache
        /// Update the iterator into the current pcode cache, and if necessary, generate
        /// the pcode for the fallthru instruction and reset the iterator.
        protected override void fallthruOp()
        {
            instruction_start = false;
            current_op += 1;
            if (current_op >= opcache.size())
            {
                current_address = current_address + instruction_length;
                createInstruction(current_address);
            }
            establishOp();
        }

        /// Execute branch (including relative branches)
        /// Since the full instruction is cached, we can do relative branches properly
        protected override void executeBranch()
        {
            Address destaddr = currentOp->getInput(0)->getAddr();
            if (destaddr.isConstant())
            {
                uintm id = destaddr.getOffset();
                id = id + (uintm)current_op;
                current_op = id;
                if (current_op == opcache.size())
                    fallthruOp();
                else if ((current_op < 0) || (current_op >= opcache.size()))
                    throw new LowlevelError("Bad intra-instruction branch");
            }
            else
                setExecuteAddress(destaddr);
        }

        /// Execute breakpoint for this user-defined op
        /// Look for a breakpoint for the given user-defined op and invoke it.
        /// If it doesn't exist, or doesn't replace the action, throw an exception
        protected override void executeCallother()
        {
            if (!breaktable->doPcodeOpBreak(currentOp))
                throw new LowlevelError("Userop not hooked");
            fallthruOp();
        }

        /// Pcode cache emulator constructor
        /// \param t is the SLEIGH translator
        /// \param s is the MemoryState the emulator should manipulate
        /// \param b is the table of breakpoints the emulator should invoke
        public EmulatePcodeCache(Translate t, MemoryState s, BreakTable b)
            : base(s)
        {
            trans = t;
            OpBehavior::registerInstructions(inst, t);
            breaktable = b;
            breaktable->setEmulate(this);
        }

        ~EmulatePcodeCache()
        {
            clearCache();
            for (int4 i = 0; i < inst.size(); ++i)
            {
                OpBehavior* t_op = inst[i];
                if (t_op != (OpBehavior*)0)
                    delete t_op;
            }
        }

        /// Return \b true if we are at an instruction start
        /// Since the emulator can single step through individual pcode operations, the machine state
        /// may be halted in the \e middle of a single machine instruction, unlike conventional debuggers.
        /// This routine can be used to determine if execution is actually at the beginning of a machine
        /// instruction.
        /// \return \b true if the next pcode operation is at the start of the instruction translation
        public bool isInstructionStart()
        {
            return instruction_start;
        }

        /// Return number of pcode ops in translation of current instruction
        /// A typical machine instruction translates into a sequence of pcode ops.
        /// \return the number of ops in the sequence
        public int numCurrentOps()
        {
            return opcache.size();
        }

        /// Get the index of current pcode op within current instruction
        /// This routine can be used to determine where, within the sequence of ops in the translation
        /// of the entire machine instruction, the currently executing op is.
        /// \return the index of the current (next) pcode op.
        public int getCurrentOpIndex()
        {
            return current_op;
        }

        /// Get pcode op in current instruction translation by index
        /// This routine can be used to examine ops other than the currently executing op in the
        /// machine instruction's translation sequence.
        /// \param i is the desired op index
        /// \return the pcode op at the indicated index
        public PcodeOpRaw getOpByIndex(int i)
        {
            return opcache[i];
        }

        /// Set current execution address
        /// Set the current execution address and cache the pcode translation of the machine instruction
        /// at that address
        /// \param addr is the address where execution should continue
        public override void setExecuteAddress(Address addr)
        {
            current_address = addr; // Copy -addr- BEFORE calling createInstruction
                                    // as it calls clear and may delete -addr-
            createInstruction(current_address);
            establishOp();
        }

        /// Get current execution address
        /// \return the currently executing machine address
        public override Address getExecuteAddress()
        {
            return current_address;
        }

        /// Execute (the rest of) a single machine instruction
        /// This routine executes an entire machine instruction at once, as a conventional debugger step
        /// function would do.  If execution is at the start of an instruction, the breakpoints are checked
        /// and invoked as needed for the current address.  If this routine is invoked while execution is
        /// in the middle of a machine instruction, execution is continued until the current instruction
        /// completes.
        public void executeInstruction()
        {
            if (instruction_start)
            {
                if (breaktable->doAddressBreak(current_address))
                    return;
            }
            do
            {
                executeCurrentOp();
            } while (!instruction_start);
        }
    }
}
