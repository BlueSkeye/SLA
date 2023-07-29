using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Sla.DECCORE
{
    internal class RuleSlessToLess : Rule
    {
        public RuleSlessToLess(string g)
            : base(g, 0, "slesstoless")
        {
        }

        public override Rule clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule*)0;
            return new RuleSlessToLess(getGroup());
        }

        /// \class RuleSlessToLess
        /// \brief Convert INT_SLESS to INT_LESS when comparing positive values
        ///
        /// This also works converting INT_SLESSEQUAL to INT_LESSEQUAL.
        /// We use the non-zero mask to verify the sign bit is zero.
        public override void getOpList(List<uint> oplist)
        {
            oplist.push_back(CPUI_INT_SLESS);
            oplist.push_back(CPUI_INT_SLESSEQUAL);
        }

        public override int applyOp(PcodeOp op, Funcdata data)
        {
            Varnode* vn = op.getIn(0);
            int sz = vn.getSize();
            if (signbit_negative(vn.getNZMask(), sz)) return 0;
            if (signbit_negative(op.getIn(1).getNZMask(), sz)) return 0;

            if (op.code() == CPUI_INT_SLESS)
                data.opSetOpcode(op, CPUI_INT_LESS);
            else
                data.opSetOpcode(op, CPUI_INT_LESSEQUAL);
            return 1;
        }
    }
}
