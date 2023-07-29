using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Sla.DECCORE
{
    internal class RuleSegment : Rule
    {
        public RuleSegment(string g)
            : base(g, 0, "segment")
        {
        }

        public override Rule clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule*)0;
            return new RuleSegment(getGroup());
        }

        /// \class RuleSegment
        /// \brief Propagate constants through a SEGMENTOP
        public override void getOpList(List<uint4> oplist)
        {
            oplist.push_back(CPUI_SEGMENTOP);
        }

        public override int4 applyOp(PcodeOp op, Funcdata data)
        {
            SegmentOp* segdef = data.getArch().userops.getSegmentOp(op.getIn(0).getSpaceFromConst().getIndex());
            if (segdef == (SegmentOp*)0)
                throw new LowlevelError("Segment operand missing definition");

            Varnode* vn1 = op.getIn(1);
            Varnode* vn2 = op.getIn(2);

            if (vn1.isConstant() && vn2.isConstant())
            {
                List<uintb> bindlist;
                bindlist.push_back(vn1.getOffset());
                bindlist.push_back(vn2.getOffset());
                uintb val = segdef.execute(bindlist);
                data.opRemoveInput(op, 2);
                data.opRemoveInput(op, 1);
                data.opSetInput(op, data.newConstant(op.getOut().getSize(), val), 0);
                data.opSetOpcode(op, CPUI_COPY);
                return 1;
            }
            else if (segdef.hasFarPointerSupport())
            {
                // If the hi and lo pieces come from a contigouous source
                if (!contiguous_test(vn1, vn2)) return 0;
                Varnode* whole = findContiguousWhole(data, vn1, vn2);
                if (whole == (Varnode*)0) return 0;
                if (whole.isFree()) return 0;
                // Use the contiguous source as the whole pointer
                data.opRemoveInput(op, 2);
                data.opRemoveInput(op, 1);
                data.opSetInput(op, whole, 0);
                data.opSetOpcode(op, CPUI_COPY);
                return 1;
            }
            return 0;
        }
    }
}
