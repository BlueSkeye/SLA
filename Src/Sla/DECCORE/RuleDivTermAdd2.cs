using Sla.CORE;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Sla.DECCORE
{
    internal class RuleDivTermAdd2 : Rule
    {
        public RuleDivTermAdd2(string g)
            : base(g, 0, "divtermadd2")
        {
        }

        public override Rule? clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule)null;
            return new RuleDivTermAdd2(getGroup());
        }

        /// \class RuleDivTermAdd2
        /// \brief Simplify another expression associated with optimized division
        ///
        /// With `W = sub( zext(V)*c, d)` the rule is:
        ///   - `W+((V-W)>>1)   =>   `sub( (zext(V)*(c+2^n))>>(n+1), 0)`
        ///
        /// where n = d*8. All extensions and right-shifts must be unsigned
        /// n must be equal to the size of SUBPIECE's truncation.
        public override void getOpList(List<OpCode> oplist)
        {
            oplist.Add(OpCode.CPUI_INT_RIGHT);
        }

        public override int applyOp(PcodeOp op, Funcdata data)
        {
            if (!op.getIn(1).isConstant())
                return 0;
            if (op.getIn(1).getOffset() != 1)
                return 0;
            if (!op.getIn(0).isWritten())
                return 0;
            PcodeOp subop = op.getIn(0).getDef() ?? throw new ApplicationException();
            if (subop.code() != OpCode.CPUI_INT_ADD)
                return 0;
            Varnode? x = (Varnode)null;
            Varnode compvn;
            PcodeOp compop;
            int i = 0;
            do {
                compvn = subop.getIn(i);
                if (compvn.isWritten()) {
                    compop = compvn.getDef();
                    if (compop.code() == OpCode.CPUI_INT_MULT) {
                        Varnode invn = compop.getIn(1);
                        if (invn.isConstant()) {
                            if (invn.getOffset() == Globals.calc_mask((uint)invn.getSize())) {
                                x = subop.getIn(1 - i);
                                break;
                            }
                        }
                    }
                }
            } while(++i < 2);
            if (i == 2) 
                return 0;
            Varnode z = compvn.getDef().getIn(0) ?? throw new ApplicationException();
            if (!z.isWritten()) return 0;
            PcodeOp subpieceop = z.getDef() ?? throw new ApplicationException();
            if (subpieceop.code() != OpCode.CPUI_SUBPIECE) return 0;
            int n = (int)(subpieceop.getIn(1).getOffset() * 8);
            if (n != 8 * (subpieceop.getIn(0).getSize() - z.getSize())) return 0;
            Varnode multvn = subpieceop.getIn(0);
            if (!multvn.isWritten()) return 0;
            PcodeOp multop = multvn.getDef();
            if (multop.code() != OpCode.CPUI_INT_MULT) return 0;
            if (!multop.getIn(1).isConstant()) return 0;
            Varnode zextvn = multop.getIn(0);
            if (!zextvn.isWritten()) return 0;
            PcodeOp zextop = zextvn.getDef();
            if (zextop.code() != OpCode.CPUI_INT_ZEXT) return 0;
            if (zextop.getIn(0) != x) return 0;

            IEnumerator<PcodeOp> iter = op.getOut().beginDescend();
            while (iter.MoveNext()) {
                PcodeOp addop = iter.Current;
                if (addop.code() != OpCode.CPUI_INT_ADD) continue;
                if ((addop.getIn(0) != z) && (addop.getIn(1) != z)) continue;

                ulong pow = 1;
                pow <<= n;          // Calculate 2^n
                ulong newc = multop.getIn(1).getOffset() + pow;
                PcodeOp newmultop = data.newOp(2, op.getAddr());
                data.opSetOpcode(newmultop, OpCode.CPUI_INT_MULT);
                Varnode newmultvn = data.newUniqueOut(zextvn.getSize(), newmultop);
                data.opSetInput(newmultop, zextvn, 0);
                data.opSetInput(newmultop, data.newConstant(zextvn.getSize(), newc), 1);
                data.opInsertBefore(newmultop, op);

                PcodeOp newshiftop = data.newOp(2, op.getAddr());
                data.opSetOpcode(newshiftop, OpCode.CPUI_INT_RIGHT);
                Varnode newshiftvn = data.newUniqueOut(zextvn.getSize(), newshiftop);
                data.opSetInput(newshiftop, newmultvn, 0);
                data.opSetInput(newshiftop, data.newConstant(4, (ulong)(n + 1)), 1);
                data.opInsertBefore(newshiftop, op);

                data.opSetOpcode(addop, OpCode.CPUI_SUBPIECE);
                data.opSetInput(addop, newshiftvn, 0);
                data.opSetInput(addop, data.newConstant(4, 0), 1);
                return 1;
            }
            return 0;
        }
    }
}
