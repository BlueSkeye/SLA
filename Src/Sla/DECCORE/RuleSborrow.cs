using Sla.CORE;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.Intrinsics.X86;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Sla.DECCORE
{
    internal class RuleSborrow : Rule
    {
        public RuleSborrow(string g)
            : base(g, 0, "sborrow")
        {
        }

        public override Rule? clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule)null;
            return new RuleSborrow(getGroup());
        }

        /// \class RuleSborrow
        /// \brief Simplify signed comparisons using INT_SBORROW
        ///
        /// - `sborrow(V,0)  =>  false`
        /// - `sborrow(V,W) != (V + (W * -1) s< 0)  =>  V s< W`
        /// - `sborrow(V,W) != (0 s< V + (W * -1))  =>  W s< V`
        /// - `sborrow(V,W) == (0 s< V + (W * -1))  =>  V s<= W`
        /// - `sborrow(V,W) == (V + (W * -1) s< 0)  =>  W s<= V`
        ///
        /// Supports variations where W is constant.
        public override void getOpList(List<OpCode> oplist)
        {
            oplist.Add(OpCode.CPUI_INT_SBORROW);
        }

        public override bool applyOp(PcodeOp op, Funcdata data)
        {
            Varnode svn = op.getOut();
            Varnode cvn;
            Varnode avn;
            Varnode bvn;
            PcodeOp compop;
            PcodeOp signop;
            PcodeOp addop;
            int zside;

            // Check for trivial case
            if ((op.getIn(1).isConstant() && op.getIn(1).getOffset() == 0) ||
                (op.getIn(0).isConstant() && op.getIn(0).getOffset() == 0))
            {
                data.opSetOpcode(op, OpCode.CPUI_COPY);
                data.opSetInput(op, data.newConstant(1, 0), 0);
                data.opRemoveInput(op, 1);
                return 1;
            }
            IEnumerator<PcodeOp> iter = svn.beginDescend();
            while (iter.MoveNext()) {
                compop = iter.Current;
                if ((compop.code() != OpCode.CPUI_INT_EQUAL) && (compop.code() != OpCode.CPUI_INT_NOTEQUAL))
                    continue;
                cvn = (compop.getIn(0) == svn) ? compop.getIn(1) : compop.getIn(0);
                if (!cvn.isWritten()) continue;
                signop = cvn.getDef();
                if (signop.code() != OpCode.CPUI_INT_SLESS) continue;
                if (!signop.getIn(0).constantMatch(0)) {
                    if (!signop.getIn(1).constantMatch(0)) continue;
                    zside = 1;
                }
                else
                    zside = 0;
                if (!signop.getIn(1 - zside).isWritten()) continue;
                addop = signop.getIn(1 - zside).getDef();
                if (addop.code() == OpCode.CPUI_INT_ADD) {
                    avn = op.getIn(0);
                    if (functionalEquality(avn, addop.getIn(0)))
                        bvn = addop.getIn(1);
                    else if (functionalEquality(avn, addop.getIn(1)))
                        bvn = addop.getIn(0);
                    else
                        continue;
                }
                else
                    continue;
                if (bvn.isConstant()) {
                    Address flip = new Address(bvn.getSpace(), Globals.uintb_negate(bvn.getOffset() - 1, bvn.getSize()));
                    bvn = op.getIn(1);
                    if (flip != bvn.getAddr()) continue;
                }
                else if (bvn.isWritten()) {
                    PcodeOp otherop = bvn.getDef();
                    if (otherop.code() == OpCode.CPUI_INT_MULT) {
                        if (!otherop.getIn(1).isConstant()) continue;
                        if (otherop.getIn(1).getOffset() != Globals.calc_mask(otherop.getIn(1).getSize())) continue;
                        bvn = otherop.getIn(0);
                    }
                    else if (otherop.code() == OpCode.CPUI_INT_2COMP)
                        bvn = otherop.getIn(0);
                    if (!functionalEquality(bvn, op.getIn(1))) continue;
                }
                else
                    continue;
                if (compop.code() == OpCode.CPUI_INT_NOTEQUAL) {
                    data.opSetOpcode(compop, OpCode.CPUI_INT_SLESS);   // Replace all this with simple less than
                    data.opSetInput(compop, avn, 1 - zside);
                    data.opSetInput(compop, bvn, zside);
                }
                else {
                    data.opSetOpcode(compop, OpCode.CPUI_INT_SLESSEQUAL);
                    data.opSetInput(compop, avn, zside);
                    data.opSetInput(compop, bvn, 1 - zside);
                }
                return 1;
            }
            return 0;
        }
    }
}
