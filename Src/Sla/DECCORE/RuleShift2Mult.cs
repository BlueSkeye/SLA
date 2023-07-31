using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Sla.DECCORE
{
    internal class RuleShift2Mult : Rule
    {
        public RuleShift2Mult(string g)
            : base(g, 0, "shift2mult")
        {
        }

        public override Rule clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule*)0;
            return new RuleShift2Mult(getGroup());
        }

        /// \class RuleShift2Mult
        /// \brief Convert INT_LEFT to INT_MULT:  `V << 2  =>  V * 4`
        ///
        /// This only applies if the result is involved in an arithmetic expression.
        public override void getOpList(List<uint> oplist)
        {
            oplist.Add(CPUI_INT_LEFT);
        }

        public override int applyOp(PcodeOp op, Funcdata data)
        {
            int flag;
            list<PcodeOp*>::const_iterator desc;
            Varnode* vn,*constvn;
            PcodeOp* arithop;
            OpCode opc;
            int val;

            flag = 0;
            vn = op.getOut();
            constvn = op.getIn(1);
            if (!constvn.isConstant()) return 0; // Shift amount must be a constant
            val = constvn.getOffset();
            if (val >= 32)      // FIXME: This is a little arbitrary. Anything
                                // this big is probably not an arithmetic multiply
                return 0;
            arithop = op.getIn(0).getDef();
            desc = vn.beginDescend();
            for (; ; )
            {
                if (arithop != (PcodeOp)null)
                {
                    opc = arithop.code();
                    if ((opc == OpCode.CPUI_INT_ADD) || (opc == OpCode.CPUI_INT_SUB) || (opc == OpCode.CPUI_INT_MULT))
                    {
                        flag = 1;
                        break;
                    }
                }
                if (desc == vn.endDescend()) break;
                arithop = *desc++;
            }

            if (flag == 0) return 0;
            constvn = data.newConstant(vn.getSize(), ((ulong)1) << val);
            data.opSetInput(op, constvn, 1);
            data.opSetOpcode(op, OpCode.CPUI_INT_MULT);
            return 1;
        }
    }
}
