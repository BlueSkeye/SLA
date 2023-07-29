using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    internal class OptionIgnoreUnimplemented : ArchOption
    {
        public OptionIgnoreUnimplemented()
        {
            name = "ignoreunimplemented";
        }

        /// \class OptionIgnoreUnimplemented
        /// \brief Toggle whether unimplemented instructions are treated as a \e no-operation
        ///
        /// If the first parameter is "on", unimplemented instructions are ignored, otherwise
        /// they are treated as an artificial \e halt in the control flow.
        public override string apply(Architecture glb, string p1, string p2, string p3)
        {
            bool val = onOrOff(p1);

            string res;
            if (val)
            {
                res = "Unimplemented instructions are now ignored (treated as nop)";
                glb.flowoptions |= FlowInfo::ignore_unimplemented;
            }
            else
            {
                res = "Unimplemented instructions now generate warnings";
                glb.flowoptions &= ~((uint4)FlowInfo::ignore_unimplemented);
            }

            return res;
        }
    }
}
