using Sla.CORE;
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
    internal class RuleLessEqual : Rule
    {
        public RuleLessEqual(string g)
            : base(g, 0, "lessequal")
        {
        }

        public override Rule? clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule)null;
            return new RuleLessEqual(getGroup());
        }

        /// \class RuleLessEqual
        /// \brief Simplify 'less than or equal':  `V < W || V == W  =>  V <= W`
        ///
        /// Similarly: `V < W || V != W  =>  V != W`
        ///
        /// Handle INT_SLESS variants as well.
        public override void getOpList(List<OpCode> oplist)
        {
            oplist.Add(OpCode.CPUI_BOOL_OR);
        }

        public override bool applyOp(PcodeOp op, Funcdata data)
        {
            Varnode compvn1;
            Varnode compvn2;
            Varnode vnout1;
            Varnode vnout2;
            PcodeOp op_less;
            PcodeOp op_equal;
            OpCode opc, equalopc;

            vnout1 = op.getIn(0);
            if (!vnout1.isWritten()) return 0;
            vnout2 = op.getIn(1);
            if (!vnout2.isWritten()) return 0;
            op_less = vnout1.getDef();
            opc = op_less.code();
            if ((opc != OpCode.CPUI_INT_LESS) && (opc != OpCode.CPUI_INT_SLESS))
            {
                op_equal = op_less;
                op_less = vnout2.getDef();
                opc = op_less.code();
                if ((opc != OpCode.CPUI_INT_LESS) && (opc != OpCode.CPUI_INT_SLESS))
                    return 0;
            }
            else
                op_equal = vnout2.getDef();
            equalopc = op_equal.code();
            if ((equalopc != OpCode.CPUI_INT_EQUAL) && (equalopc != OpCode.CPUI_INT_NOTEQUAL))
                return 0;

            compvn1 = op_less.getIn(0);
            compvn2 = op_less.getIn(1);
            if (!compvn1.isHeritageKnown()) return 0;
            if (!compvn2.isHeritageKnown()) return 0;
            if (((*compvn1 != *op_equal.getIn(0)) || (*compvn2 != *op_equal.getIn(1))) &&
                ((*compvn1 != *op_equal.getIn(1)) || (*compvn2 != *op_equal.getIn(0))))
                return 0;

            if (equalopc == OpCode.CPUI_INT_NOTEQUAL)
            { // op_less is redundant
                data.opSetOpcode(op, OpCode.CPUI_COPY); // Convert OR to COPY
                data.opRemoveInput(op, 1);
                data.opSetInput(op, op_equal.getOut(), 0); // Taking the NOTEQUAL output
            }
            else
            {
                data.opSetInput(op, compvn1, 0);
                data.opSetInput(op, compvn2, 1);
                data.opSetOpcode(op, (opc == OpCode.CPUI_INT_SLESS) ? OpCode.CPUI_INT_SLESSEQUAL : OpCode.CPUI_INT_LESSEQUAL);
            }

            return 1;
        }
    }
}
