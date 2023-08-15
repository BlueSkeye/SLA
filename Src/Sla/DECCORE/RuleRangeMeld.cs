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
    internal class RuleRangeMeld : Rule
    {
        public RuleRangeMeld(string g)
            : base(g, 0, "rangemeld")
        {
        }

        public override Rule? clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule)null;
            return new RuleRangeMeld(getGroup());
        }

        public override void getOpList(List<OpCode> oplist)
        {
            oplist.Add(OpCode.CPUI_BOOL_OR);
            oplist.Add(OpCode.CPUI_BOOL_AND);
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
        public override bool applyOp(PcodeOp op, Funcdata data)
        {
            Varnode vn1, vn2;
            int restype;

            vn1 = op.getIn(0);
            if (!vn1.isWritten()) return 0;
            vn2 = op.getIn(1);
            if (!vn2.isWritten()) return 0;
            PcodeOp sub1 = vn1.getDef() ?? throw new BugException();
            if (!sub1.isBoolOutput())
                return 0;
            PcodeOp sub2 = vn2.getDef() ?? throw new BugException();
            if (!sub2.isBoolOutput())
                return 0;

            CircleRange range1 = new CircleRange(true);
            Varnode? markup = (Varnode)null;
            Varnode? A1 = range1.pullBack(sub1, out markup, false);
            if (A1 == (Varnode)null) return 0;
            CircleRange range2 = new CircleRange(true);
            Varnode? A2 = range2.pullBack(sub2, out markup, false);
            if (A2 == (Varnode)null) return 0;
            if (sub1.code() == OpCode.CPUI_BOOL_NEGATE) {
                // Do an extra pull back, if the last step is a '!'
                if (!A1.isWritten()) return 0;
                A1 = range1.pullBack(A1.getDef(), out markup, false);
                if (A1 == (Varnode)null) return 0;
            }
            if (sub2.code() == OpCode.CPUI_BOOL_NEGATE) {
                // Do an extra pull back, if the last step is a '!'
                if (!A2.isWritten()) return 0;
                A2 = range2.pullBack(A2.getDef(), out markup, false);
                if (A2 == (Varnode)null) return 0;
            }
            if (!functionalEquality(A1, A2)) {
                if (A2.getSize() == A1.getSize()) return 0;
                if ((A1.getSize() < A2.getSize()) && (A2.isWritten()))
                    A2 = range2.pullBack(A2.getDef(), out markup, false);
                else if (A1.isWritten())
                    A1 = range1.pullBack(A1.getDef(), out markup, false);
                if (A1 != A2) return 0;
            }
            if (!A1.isHeritageKnown()) return 0;

            if (op.code() == OpCode.CPUI_BOOL_AND)
                restype = range1.intersect(range2);
            else
                restype = range1.circleUnion(range2);

            if (restype == 0) {
                OpCode opc;
                ulong resc;
                int resslot;
                restype = range1.translate2Op(out opc, out resc, out resslot);
                if (restype == 0) {
                    Varnode newConst = data.newConstant(A1.getSize(), resc);
                    if (markup != (Varnode)null) {
                        // We have potential constant markup
                        newConst.copySymbolIfValid(markup);    // Propagate the markup into our new constant
                    }
                    data.opSetOpcode(op, opc);
                    data.opSetInput(op, A1, 1 - resslot);
                    data.opSetInput(op, newConst, resslot);
                    return 1;
                }
            }

            if (restype == 2) return 0; // Cannot represent
            if (restype == 1) {
                // Pieces covers everything, condition is always true
                data.opSetOpcode(op, OpCode.CPUI_COPY);
                data.opRemoveInput(op, 1);
                data.opSetInput(op, data.newConstant(1, 1), 0);
            }
            else if (restype == 3) {
                // Nothing left in intersection, condition is always false
                data.opSetOpcode(op, OpCode.CPUI_COPY);
                data.opRemoveInput(op, 1);
                data.opSetInput(op, data.newConstant(1, 0), 0);
            }
            return 1;
        }
    }
}
