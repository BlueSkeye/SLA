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
    internal class RuleIntLessEqual : Rule
    {
        public RuleIntLessEqual(string g)
            : base(g, 0, "intlessequal")
        {
        }

        public override Rule clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule*)0;
            return new RuleIntLessEqual(getGroup());
        }

        /// \class RuleIntLessEqual
        /// \brief Convert LESSEQUAL to LESS:  `V <= c  =>  V < (c+1)`
        public override void getOpList(List<uint4> oplist)
        {
            oplist.push_back(CPUI_INT_LESSEQUAL);
            oplist.push_back(CPUI_INT_SLESSEQUAL);
        }

        public override int4 applyOp(PcodeOp op, Funcdata data)
        {
            if (data.replaceLessequal(op))
                return 1;
            return 0;
        }
    }
}
