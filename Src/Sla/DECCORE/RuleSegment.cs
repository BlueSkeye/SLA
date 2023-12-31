﻿using Sla.CORE;

namespace Sla.DECCORE
{
    internal class RuleSegment : Rule
    {
        public RuleSegment(string g)
            : base(g, 0, "segment")
        {
        }

        public override Rule? clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule)null;
            return new RuleSegment(getGroup());
        }

        /// \class RuleSegment
        /// \brief Propagate constants through a SEGMENTOP
        public override void getOpList(List<OpCode> oplist)
        {
            oplist.Add(OpCode.CPUI_SEGMENTOP);
        }

        public override int applyOp(PcodeOp op, Funcdata data)
        {
            SegmentOp segdef = data.getArch().userops.getSegmentOp(op.getIn(0).getSpaceFromConst().getIndex());
            if (segdef == (SegmentOp)null)
                throw new LowlevelError("Segment operand missing definition");

            Varnode vn1 = op.getIn(1);
            Varnode vn2 = op.getIn(2);

            if (vn1.isConstant() && vn2.isConstant()) {
                List<ulong> bindlist = new List<ulong>();
                bindlist.Add(vn1.getOffset());
                bindlist.Add(vn2.getOffset());
                ulong val = segdef.execute(bindlist);
                data.opRemoveInput(op, 2);
                data.opRemoveInput(op, 1);
                data.opSetInput(op, data.newConstant(op.getOut().getSize(), val), 0);
                data.opSetOpcode(op, OpCode.CPUI_COPY);
                return 1;
            }
            else if (segdef.hasFarPointerSupport()) {
                // If the hi and lo pieces come from a contigouous source
                if (!Globals.contiguous_test(vn1, vn2)) return 0;
                Varnode whole = Globals.findContiguousWhole(data, vn1, vn2);
                if (whole == (Varnode)null) return 0;
                if (whole.isFree()) return 0;
                // Use the contiguous source as the whole pointer
                data.opRemoveInput(op, 2);
                data.opRemoveInput(op, 1);
                data.opSetInput(op, whole, 0);
                data.opSetOpcode(op, OpCode.CPUI_COPY);
                return 1;
            }
            return 0;
        }
    }
}
