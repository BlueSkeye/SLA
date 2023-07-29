using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief A snippet of p-code that can be executed outside of normal analysis
    ///
    /// Essentially a p-code script.  The p-code contained in this snippet needs to be
    /// processor agnostic, so any register Varnodes must be temporary (out of the \e unique space)
    /// and any control-flow operations must be contained within the snippet (p-code relative addressing).
    /// Input and output to the snippet/script is provided by standard injection parameters.
    /// The class contains, as a field, a stripped down emulator to run the script and
    /// a convenience method evaluate() to feed in concrete values to the input parameters
    /// and return a value from a single output parameter.
    internal class ExecutablePcode : InjectPayload
    {
        /// The Architecture owning \b this snippet
        private Architecture glb;
        /// Description of the source of \b this snippet
        private string source;
        /// Whether build() method has run, setting up the emulator
        private bool built;
        /// The emulator
        private EmulateSnippet emulator;
        /// Temporary ids of input varnodes
        private List<uintb> inputList;
        /// Temporary ids of output varnodes
        private List<uintb> outputList;
        /// Emitter (allocated temporarily) for initializing the emulator
        private PcodeEmit emitter;

        /// Initialize the Emulate object with the snippet p-code
        private void build()
        {
            if (built) return;
            InjectContext & icontext(glb.pcodeinjectlib.getCachedContext());
            icontext.clear();
            uintb uniqReserve = 0x10;           // Temporary register space reserved for inputs and output
            AddrSpace* codeSpace = glb.getDefaultCodeSpace();
            AddrSpace* uniqSpace = glb.getUniqueSpace();
            icontext.baseaddr = Address(codeSpace, 0x1000); // Fake address
            icontext.nextaddr = icontext.baseaddr;
            for (int4 i = 0; i < sizeInput(); ++i)
            {   // Skip the first operand containing the injectid
                InjectParameter & param(getInput(i));
                icontext.inputlist.emplace_back();
                icontext.inputlist.back().space = uniqSpace;
                icontext.inputlist.back().offset = uniqReserve;
                icontext.inputlist.back().size = param.getSize();
                inputList.push_back(uniqReserve);
                uniqReserve += 0x20;
            }
            for (int4 i = 0; i < sizeOutput(); ++i)
            {
                InjectParameter & param(getOutput(i));
                icontext.output.emplace_back();
                icontext.output.back().space = uniqSpace;
                icontext.output.back().offset = uniqReserve;
                icontext.output.back().size = param.getSize();
                outputList.push_back(uniqReserve);
                uniqReserve += 0x20;
            }
            emitter = emulator.buildEmitter(glb.pcodeinjectlib.getBehaviors(), uniqReserve);
            inject(icontext, *emitter);
            delete emitter;
            emitter = (PcodeEmit*)0;
            if (!emulator.checkForLegalCode())
                throw new LowlevelError("Illegal p-code in executable snippet");
            built = true;
        }

        /// \param g is the Architecture owning \b snippet
        /// \param src is a string describing the \e source of the snippet
        /// \param nm is the formal name of the snippet
        public ExecutablePcode(Architecture g, string src, string nm)
        {
            glb = g;
            emitter = (PcodeEmit*)0;
            source = src;
            built = false;
        }

        ~ExecutablePcode()
        {
            if (emitter != (PcodeEmit*)0) delete emitter;
        }
    
        public override string getSource() => source;

        /// Evaluate the snippet on the given inputs
        /// The caller provides a list of concrete values that are assigned to the
        /// input parameters.  The number of values and input parameters must match,
        /// and values are assigned in order. Input parameter order is determined either
        /// by the order of tags in the defining XML.  This method assumes there is
        /// exactly 1 relevant output parameter. Once the snippet is executed the
        /// value of this parameter is read from the emulator state and returned.
        /// \param input is the ordered list of input values to feed to \b this script
        /// \return the value of the output parameter after script execution
        public uintb evaluate(List<uintb> input)
        {
            build();        // Build the PcodeOpRaws (if we haven't before)
            emulator.resetMemory();
            if (input.size() != inputList.size())
                throw new LowlevelError("Wrong number of input parameters to executable snippet");
            if (outputList.size() == 0)
                throw new LowlevelError("No registered outputs to executable snippet");
            for (int4 i = 0; i < input.size(); ++i)
                emulator.setVarnodeValue(inputList[i], input[i]);
            while (!emulator.getHalt())
                emulator.executeCurrentOp();
            return emulator.getTempValue(outputList[0]);
        }
    }
}
