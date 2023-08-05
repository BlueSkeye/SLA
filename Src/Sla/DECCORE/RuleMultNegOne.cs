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
    internal class RuleMultNegOne : Rule
    {
        public RuleMultNegOne(string g)
            : base(g, 0, "multnegone")
        {
        }

        public override Rule clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule)null;
            return new RuleMultNegOne(getGroup());
        }

        /// \class RuleMultNegOne
        /// \brief Cleanup: Convert INT_2COMP from INT_MULT:  `V * -1  =>  -V`
        public override void getOpList(List<OpCode> oplist)
        {
            oplist.Add(CPUI_INT_MULT);
        }

        public override int applyOp(PcodeOp op, Funcdata data)
        {               // a * -1 . -a
            Varnode* constvn = op.getIn(1);

            if (!constvn.isConstant()) return 0;
            if (constvn.getOffset() != Globals.calc_mask(constvn.getSize())) return 0;

            data.opSetOpcode(op, OpCode.CPUI_INT_2COMP);
            data.opRemoveInput(op, 1);
            return 1;
        }
    }
}
