using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    internal class OptionNoCastPrinting : ArchOption
    {
        public OptionNoCastPrinting()
        {
            name = "nocastprinting";
        }

        /// \class OptionNoCastPrinting
        /// \brief Toggle whether \e cast syntax is emitted by the decompiler or stripped
        public override string apply(Architecture glb, string p1, string p2, string p3)
        {
            bool val = onOrOff(p1);
            PrintC* lng = dynamic_cast<PrintC*>(glb->print);
            if (lng == (PrintC*)0)
                return "Can only set no cast printing for C language";
            lng->setNoCastPrinting(val);
            string prop;
            prop = val ? "on" : "off";
            return "No cast printing turned " + prop;
        }
    }
}
