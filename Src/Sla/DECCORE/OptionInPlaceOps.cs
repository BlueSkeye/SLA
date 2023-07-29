using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    internal class OptionInPlaceOps : ArchOption
    {
        public OptionInPlaceOps()
        {
            name = "inplaceops";
        }

        /// \class OptionInPlaceOps
        /// \brief Toggle whether \e in-place operators (+=, *=, &=, etc.) are emitted by the decompiler
        public override string apply(Architecture glb, string p1, string p2, string p3)
        {
            bool val = onOrOff(p1);
            if (glb.print.getName() != "c-language")
                return "Can only set inplace operators for C language";
            PrintC* lng = (PrintC*)glb.print;
            lng.setInplaceOps(val);
            string prop;
            prop = val ? "on" : "off";
            return "Inplace operators turned " + prop;
        }
    }
}
