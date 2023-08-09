using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief A user defined p-code op with no specialization
    ///
    /// This class is used by the manager for CALLOTHER indices that have not been
    /// mapped to a specialization. The p-code operation has the (SLEIGH assigned) name,
    /// but still has an unknown effect.
    internal class UnspecializedPcodeOp : UserPcodeOp
    {
        public UnspecializedPcodeOp(Architecture g, string nm,int ind)
            : base(g, nm, ind)
        {
        }

        public override void decode(Sla.CORE.Decoder decoder)
        {
        }
    }
}
