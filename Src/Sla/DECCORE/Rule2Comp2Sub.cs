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
    internal class Rule2Comp2Sub : Rule
    {
        public Rule2Comp2Sub(string g)
            : base(g, 0, "2comp2sub")
        {
        }

        public override Rule clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule)null;
            return new Rule2Comp2Sub(getGroup());
        }

        /// \class Rule2Comp2Sub
        /// \brief Cleanup: Convert INT_ADD back to INT_SUB: `V + -W  ==> V - W`
        public override void getOpList(List<OpCode> oplist)
        {
            oplist.Add(CPUI_INT_2COMP);
        }

        public override int applyOp(PcodeOp op, Funcdata data)
        {
            PcodeOp* addop = op.getOut().loneDescend();
            if (addop == (PcodeOp)null) return 0;
            if (addop.code() != OpCode.CPUI_INT_ADD) return 0;
            if (addop.getIn(0) == op.getOut())
                data.opSetInput(addop, addop.getIn(1), 0);
            data.opSetInput(addop, op.getIn(0), 1);
            data.opSetOpcode(addop, OpCode.CPUI_INT_SUB);
            data.opDestroy(op);     // Completely remove 2COMP
            return 1;
        }
    }
}
