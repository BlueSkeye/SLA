using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ghidra
{
    internal class OptionNullPrinting : ArchOption
    {
        public OptionNullPrinting()
        {
            name = "nullprinting";
        }

        /// \class OptionNullPrinting
        /// \brief Toggle whether null pointers should be printed as the string "NULL"
        public override string apply(Architecture glb, string p1, string p2, string p3)
        {
            bool val = onOrOff(p1);
            if (glb->print->getName() != "c-language")
                return "Only c-language accepts the null printing option";
            PrintC* lng = (PrintC*)glb->print;
            lng->setNULLPrinting(val);
            string prop;
            prop = val ? "on" : "off";
            return "Null printing turned " + prop;
        }
    }
}
