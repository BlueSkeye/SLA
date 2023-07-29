using Sla.CORE;
using Sla.DECCORE;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
#if CPUI_RULECOMPILE
    internal class DummyTranslate : Translate
    {
        public override void initialize(DocumentStorage store)
        {
        }
        public override VarnodeData getRegister(string nm) 
        {
            throw new LowlevelError("Cannot add register to DummyTranslate");
        }

        public override string getRegisterName(AddrSpace @base, uintb off, int4 size) => "";

        public override void getAllRegisters(Dictionary<VarnodeData, string> reglist)
        {
        }

        public override void getUserOpNames(List<string> res)
        {
        }

        public override int4 instructionLength(Address baseaddr) => -1;

        public override int4 oneInstruction(PcodeEmit emit, Address baseaddr) => -1;

        public override int4 printAssembly(AssemblyEmit emit, Address baseaddr) => -1;
    }
#endif
}
