using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    internal class OptionJumpLoad : ArchOption
    {
        public OptionJumpLoad()
        {
            name = "jumpload";
        }

        /// \class OptionJumpLoad
        /// \brief Toggle whether the decompiler should try to recover the table used to evaluate a switch
        ///
        /// If the first parameter is "on", the decompiler will record the memory locations with constant values
        /// that were accessed as part of the jump-table so that they can be formally labeled.
        public override string apply(Architecture glb, string p1, string p2, string p3)
        {
            bool val = onOrOff(p1);

            string res;
            if (val)
            {
                res = "Jumptable analysis will record loads required to calculate jump address";
                glb.flowoptions |= FlowInfo::record_jumploads;
            }
            else
            {
                res = "Jumptable analysis will NOT record loads";
                glb.flowoptions &= ~((uint4)FlowInfo::record_jumploads);
            }
            return res;
        }
    }
}
