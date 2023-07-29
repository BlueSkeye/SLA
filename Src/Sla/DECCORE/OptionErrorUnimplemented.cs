using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    internal class OptionErrorUnimplemented : ArchOption
    {
        public OptionErrorUnimplemented()
        {
            name = "errorunimplemented";
        }

        /// \class OptionErrorUnimplemented
        /// \brief Toggle whether unimplemented  instructions are treated as a fatal error.
        ///
        /// If the first parameter is "on", decompilation of functions with unimplemented instructions
        /// will terminate with a fatal error message. Otherwise, warning comments will be generated.
        public override string apply(Architecture glb, string p1, string p2, string p3)
        {
            bool val = onOrOff(p1);

            string res;
            if (val)
            {
                res = "Unimplemented instructions are now a fatal error";
                glb.flowoptions |= FlowInfo::error_unimplemented;
            }
            else
            {
                res = "Unimplemented instructions now NOT a fatal error";
                glb.flowoptions &= ~((uint)FlowInfo::error_unimplemented);
            }

            return res;
        }
    }
}
