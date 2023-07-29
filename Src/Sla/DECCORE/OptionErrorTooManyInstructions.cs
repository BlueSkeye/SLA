using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    internal class OptionErrorTooManyInstructions : ArchOption
    {
        public OptionErrorTooManyInstructions()
        {
            name = "errortoomanyinstructions";
        }

        /// \class OptionErrorTooManyInstructions
        /// \brief Toggle whether too many instructions in one function body is considered a fatal error.
        ///
        /// If the first parameter is "on" and the number of instructions in a single function body exceeds
        /// the threshold, then decompilation will halt for that function with a fatal error. Otherwise,
        /// artificial halts are generated to prevent control-flow into further instructions.
        public override string apply(Architecture glb, string p1, string p2, string p3)
        {
            bool val = onOrOff(p1);

            string res;
            if (val)
            {
                res = "Too many instructions are now a fatal error";
                glb.flowoptions |= FlowInfo::error_toomanyinstructions;
            }
            else
            {
                res = "Too many instructions are now NOT a fatal error";
                glb.flowoptions &= ~((uint4)FlowInfo::error_toomanyinstructions);
            }

            return res;
        }
    }
}
