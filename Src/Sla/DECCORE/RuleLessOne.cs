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
    internal class RuleLessOne : Rule
    {
        public RuleLessOne(string g)
            : base(g, 0, "lessone")
        {
        }

        public override Rule? clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule)null;
            return new RuleLessOne(getGroup());
        }

        /// \class RuleLessOne
        /// \brief Transform INT_LESS of 0 or 1:  `V < 1  =>  V == 0,  V <= 0  =>  V == 0`
        public override void getOpList(List<OpCode> oplist)
        {
            oplist.Add(OpCode.CPUI_INT_LESS);
            oplist.Add(OpCode.CPUI_INT_LESSEQUAL);
        }

        public override int applyOp(PcodeOp op, Funcdata data)
        {
            Varnode constvn = op.getIn(1);

            if (!constvn.isConstant()) return 0;
            ulong val = constvn.getOffset();
            if ((op.code() == OpCode.CPUI_INT_LESS) && (val != 1)) return 0;
            if ((op.code() == OpCode.CPUI_INT_LESSEQUAL) && (val != 0)) return 0;

            data.opSetOpcode(op, OpCode.CPUI_INT_EQUAL);
            if (val != 0)
                data.opSetInput(op, data.newConstant(constvn.getSize(), 0), 1);
            return 1;
        }
    }
}
