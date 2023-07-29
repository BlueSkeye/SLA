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
    internal class RuleRangeMeld : Rule
    {
        public RuleRangeMeld(string g)
            : base(g, 0, "rangemeld")
        {
        }

        public override Rule clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule*)0;
            return new RuleRangeMeld(getGroup());
        }

        public override void getOpList(List<uint4> oplist)
        {
            oplist.push_back(CPUI_BOOL_OR);
            oplist.push_back(CPUI_BOOL_AND);
        }

        /// \class RuleRangeMeld
        /// \brief Merge range conditions of the form: `V s< c, c s< V, V == c, V != c`
        ///
        /// Look for combinations of these forms based on BOOL_AND and BOOL_OR, such as
        ///
        ///   \<range1>&&\<range2> OR \<range1>||\<range2>
        ///
        /// Try to union or intersect the ranges to produce
        /// a more concise expression.
        public override int4 applyOp(PcodeOp op, Funcdata data)
        {
            PcodeOp* sub1,*sub2;
            Varnode* vn1,*vn2;
            Varnode* A1,*A2;
            int4 restype;

            vn1 = op->getIn(0);
            if (!vn1->isWritten()) return 0;
            vn2 = op->getIn(1);
            if (!vn2->isWritten()) return 0;
            sub1 = vn1->getDef();
            if (!sub1->isBoolOutput())
                return 0;
            sub2 = vn2->getDef();
            if (!sub2->isBoolOutput())
                return 0;

            CircleRange range1(true);
            Varnode* markup = (Varnode*)0;
            A1 = range1.pullBack(sub1, &markup, false);
            if (A1 == (Varnode*)0) return 0;
            CircleRange range2(true);
            A2 = range2.pullBack(sub2, &markup, false);
            if (A2 == (Varnode*)0) return 0;
            if (sub1->code() == CPUI_BOOL_NEGATE)
            { // Do an extra pull back, if the last step is a '!'
                if (!A1->isWritten()) return 0;
                A1 = range1.pullBack(A1->getDef(), &markup, false);
                if (A1 == (Varnode*)0) return 0;
            }
            if (sub2->code() == CPUI_BOOL_NEGATE)
            { // Do an extra pull back, if the last step is a '!'
                if (!A2->isWritten()) return 0;
                A2 = range2.pullBack(A2->getDef(), &markup, false);
                if (A2 == (Varnode*)0) return 0;
            }
            if (!functionalEquality(A1, A2))
            {
                if (A2->getSize() == A1->getSize()) return 0;
                if ((A1->getSize() < A2->getSize()) && (A2->isWritten()))
                    A2 = range2.pullBack(A2->getDef(), &markup, false);
                else if (A1->isWritten())
                    A1 = range1.pullBack(A1->getDef(), &markup, false);
                if (A1 != A2) return 0;
            }
            if (!A1->isHeritageKnown()) return 0;

            if (op->code() == CPUI_BOOL_AND)
                restype = range1.intersect(range2);
            else
                restype = range1.circleUnion(range2);

            if (restype == 0)
            {
                OpCode opc;
                uintb resc;
                int4 resslot;
                restype = range1.translate2Op(opc, resc, resslot);
                if (restype == 0)
                {
                    Varnode* newConst = data.newConstant(A1->getSize(), resc);
                    if (markup != (Varnode*)0)
                    {       // We have potential constant markup
                        newConst->copySymbolIfValid(markup);    // Propagate the markup into our new constant
                    }
                    data.opSetOpcode(op, opc);
                    data.opSetInput(op, A1, 1 - resslot);
                    data.opSetInput(op, newConst, resslot);
                    return 1;
                }
            }

            if (restype == 2) return 0; // Cannot represent
            if (restype == 1)
            {       // Pieces covers everything, condition is always true
                data.opSetOpcode(op, CPUI_COPY);
                data.opRemoveInput(op, 1);
                data.opSetInput(op, data.newConstant(1, 1), 0);
            }
            else if (restype == 3)
            {   // Nothing left in intersection, condition is always false
                data.opSetOpcode(op, CPUI_COPY);
                data.opRemoveInput(op, 1);
                data.opSetInput(op, data.newConstant(1, 0), 0);
            }
            return 1;
        }
    }
}
