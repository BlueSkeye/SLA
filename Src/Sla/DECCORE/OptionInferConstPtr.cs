using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    internal class OptionInferConstPtr : ArchOption
    {
        public OptionInferConstPtr()
        {
            name = "inferconstptr";
        }

        /// \class OptionInferConstPtr
        /// \brief Toggle whether the decompiler attempts to infer constant pointers
        ///
        /// Setting the first parameter to "on" causes the decompiler to check if unknown
        /// constants look like a reference to a known symbol's location.
        public override string apply(Architecture glb, string p1, string p2, string p3)
        {
            bool val = onOrOff(p1);

            string res;
            if (val)
            {
                res = "Constant pointers are now inferred";
                glb.infer_pointers = true;
            }
            else
            {
                res = "Constant pointers must now be set explicitly";
                glb.infer_pointers = false;
            }
            return res;
        }
    }
}
