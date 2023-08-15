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
    internal class Rule2Comp2Mult : Rule
    {
        public Rule2Comp2Mult(string g)
            : base(g,0,"2comp2mult")
        {
        }

        public override Rule? clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule)null;
            return new Rule2Comp2Mult(getGroup());
        }

        /// \class Rule2Comp2Mult
        /// \brief Eliminate INT_2COMP:  `-V  =>  V * -1`
        public override void getOpList(List<OpCode> oplist)
        {
            oplist.Add(CPUI_INT_2COMP);
        }

        public override bool applyOp(PcodeOp op, Funcdata data)
        {
            data.opSetOpcode(op, OpCode.CPUI_INT_MULT);
            int size = op.getIn(0).getSize();
            Varnode* negone = data.newConstant(size, Globals.calc_mask(size));
            data.opInsertInput(op, negone, 1);
            return 1;
        }
    }
}
