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
    internal class RuleFloatRange : Rule
    {
        public RuleFloatRange(string g)
            : base(g, 0, "floatrange")
        {
        }

        public override Rule clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule*)0;
            return new RuleFloatRange(getGroup());
        }

        /// \class RuleFloatRange
        /// \brief Merge range conditions of the form: `V f< c, c f< V, V f== c` etc.
        ///
        /// Convert `(V f< W)||(V f== W)   =>   V f<= W` (and similar variants)
        public override void getOpList(List<uint4> oplist)
        {
            oplist.push_back(CPUI_BOOL_OR);
            oplist.push_back(CPUI_BOOL_AND);
        }

        public override int4 applyOp(PcodeOp op, Funcdata data)
        {
            PcodeOp* cmp1,*cmp2;
            Varnode* vn1,*vn2;

            vn1 = op->getIn(0);
            if (!vn1->isWritten()) return 0;
            vn2 = op->getIn(1);
            if (!vn2->isWritten()) return 0;
            cmp1 = vn1->getDef();
            cmp2 = vn2->getDef();
            OpCode opccmp1 = cmp1->code();
            // Set cmp1 to LESS or LESSEQUAL operator, cmp2 is the "other" operator
            if ((opccmp1 != CPUI_FLOAT_LESS) && (opccmp1 != CPUI_FLOAT_LESSEQUAL))
            {
                cmp1 = cmp2;
                cmp2 = vn1->getDef();
                opccmp1 = cmp1->code();
            }
            OpCode resultopc = CPUI_COPY;
            if (opccmp1 == CPUI_FLOAT_LESS)
            {
                if ((cmp2->code() == CPUI_FLOAT_EQUAL) && (op->code() == CPUI_BOOL_OR))
                    resultopc = CPUI_FLOAT_LESSEQUAL;
            }
            else if (opccmp1 == CPUI_FLOAT_LESSEQUAL)
            {
                if ((cmp2->code() == CPUI_FLOAT_NOTEQUAL) && (op->code() == CPUI_BOOL_AND))
                    resultopc = CPUI_FLOAT_LESS;
            }

            if (resultopc == CPUI_COPY) return 0;

            // Make sure both operators are comparing the same things
            Varnode* nvn1,*cvn1;
            int4 slot1 = 0;
            nvn1 = cmp1->getIn(slot1);  // Set nvn1 to a non-constant off of cmp1
            if (nvn1->isConstant())
            {
                slot1 = 1;
                nvn1 = cmp1->getIn(slot1);
                if (nvn1->isConstant()) return 0;
            }
            if (nvn1->isFree()) return 0;
            cvn1 = cmp1->getIn(1 - slot1);  // Set cvn1 to the "other" slot off of cmp1
            int4 slot2;
            if (nvn1 != cmp2->getIn(0))
            {
                slot2 = 1;
                if (nvn1 != cmp2->getIn(1))
                    return 0;
            }
            else
                slot2 = 0;
            Varnode* matchvn = cmp2->getIn(1 - slot2);
            if (cvn1->isConstant())
            {
                if (!matchvn->isConstant()) return 0;
                if (matchvn->getOffset() != cvn1->getOffset()) return 0;
            }
            else if (cvn1 != matchvn)
                return 0;
            else if (cvn1->isFree())
                return 0;

            // Collapse the 2 comparisons into 1 comparison
            data.opSetOpcode(op, resultopc);
            data.opSetInput(op, nvn1, slot1);
            if (cvn1->isConstant())
                data.opSetInput(op, data.newConstant(cvn1->getSize(), cvn1->getOffset()), 1 - slot1);
            else
                data.opSetInput(op, cvn1, 1 - slot1);
            return 1;
        }
    }
}
