﻿using Sla.CORE;

namespace Sla.DECCORE
{
    internal class RuleFloatCast : Rule
    {
        public RuleFloatCast(string g)
            : base(g, 0, "floatcast")
        {
        }

        public override Rule? clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule)null;
            return new RuleFloatCast(getGroup());
        }

        /// \class RuleFloatCast
        /// \brief Replace (casttosmall)(casttobig)V with identity or with single cast
        public override void getOpList(List<OpCode> oplist)
        {
            oplist.Add(OpCode.CPUI_FLOAT_FLOAT2FLOAT);
            oplist.Add(OpCode.CPUI_FLOAT_TRUNC);
        }

        public override int applyOp(PcodeOp op, Funcdata data)
        {
            Varnode vn1 = op.getIn(0);
            if (!vn1.isWritten()) return 0;
            PcodeOp castop = vn1.getDef();
            OpCode opc2 = castop.code();
            if ((opc2 != OpCode.CPUI_FLOAT_FLOAT2FLOAT) && (opc2 != OpCode.CPUI_FLOAT_INT2FLOAT))
                return 0;
            OpCode opc1 = op.code();
            Varnode vn2 = castop.getIn(0);
            int insize1 = vn1.getSize();
            int insize2 = vn2.getSize();
            int outsize = op.getOut().getSize();

            if (vn2.isFree()) return 0;    // Don't propagate free

            if ((opc2 == OpCode.CPUI_FLOAT_FLOAT2FLOAT) && (opc1 == OpCode.CPUI_FLOAT_FLOAT2FLOAT)) {
                if (insize1 > outsize) {   // op is superfluous
                    data.opSetInput(op, vn2, 0);
                    if (outsize == insize2)
                        data.opSetOpcode(op, OpCode.CPUI_COPY);    // We really have the identity
                    return 1;
                }
                else if (insize2 < insize1) {
                    // Convert two increases . one combined increase
                    data.opSetInput(op, vn2, 0);
                    return 1;
                }
            }
            else if ((opc2 == OpCode.CPUI_FLOAT_INT2FLOAT) && (opc1 == OpCode.CPUI_FLOAT_FLOAT2FLOAT)) {
                // Convert integer straight into final float size
                data.opSetInput(op, vn2, 0);
                data.opSetOpcode(op, OpCode.CPUI_FLOAT_INT2FLOAT);
                return 1;
            }
            else if ((opc2 == OpCode.CPUI_FLOAT_FLOAT2FLOAT) && (opc1 == OpCode.CPUI_FLOAT_TRUNC)) {
                // Convert float straight into final integer
                data.opSetInput(op, vn2, 0);
                return 1;
            }

            return 0;
        }
    }
}
