﻿using Sla.CORE;

namespace Sla.DECCORE
{
    internal class RuleXorCollapse : Rule
    {
        public RuleXorCollapse(string g)
            : base(g, 0, "xorcollapse")
        {
        }

        public override Rule? clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule)null;
            return new RuleXorCollapse(getGroup());
        }

        /// \class RuleXorCollapse
        /// \brief Eliminate INT_XOR in comparisons: `(V ^ W) == 0  =>  V == W`
        ///
        /// The comparison can be INT_EQUAL or INT_NOTEQUAL. This also supports the form:
        ///   - `(V ^ c) == d  => V == (c^d)`
        public override void getOpList(List<OpCode> oplist)
        {
            oplist.Add(OpCode.CPUI_INT_EQUAL);
            oplist.Add(OpCode.CPUI_INT_NOTEQUAL);
        }

        public override int applyOp(PcodeOp op, Funcdata data)
        {
            ulong coeff1, coeff2;

            if (!op.getIn(1).isConstant()) return 0;
            PcodeOp xorop = op.getIn(0).getDef();
            if (xorop == (PcodeOp)null) return 0;
            if (xorop.code() != OpCode.CPUI_INT_XOR) return 0;
            if (op.getIn(0).loneDescend() == (PcodeOp)null) return 0;
            coeff1 = op.getIn(1).getOffset();
            Varnode xorvn = xorop.getIn(1);
            if (xorop.getIn(0).isFree()) return 0; // This will be propagated
            if (!xorvn.isConstant()) {
                if (coeff1 != 0) return 0;
                if (xorvn.isFree()) return 0;
                data.opSetInput(op, xorvn, 1); // Move term to other side
                data.opSetInput(op, xorop.getIn(0), 0);
                return 1;
            }
            coeff2 = xorvn.getOffset();
            if (coeff2 == 0) return 0;
            Varnode constvn = data.newConstant(op.getIn(1).getSize(), coeff1 ^ coeff2);
            constvn.copySymbolIfValid(xorvn);
            data.opSetInput(op, constvn, 1);
            data.opSetInput(op, xorop.getIn(0), 0);
            return 1;
        }
    }
}
