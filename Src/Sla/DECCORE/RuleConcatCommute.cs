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
    internal class RuleConcatCommute : Rule
    {
        public RuleConcatCommute(string g)
            : base(g, 0, "concatcommute")
        {
        }

        public override Rule clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule*)0;
            return new RuleConcatCommute(getGroup());
        }

        /// \class RuleConcatCommute
        /// \brief Commute PIECE with INT_AND, INT_OR, and INT_XOR
        ///
        /// This supports forms:
        ///   - `concat( V & c, W)  =>  concat(V,W) & (c<<16 | 0xffff)`
        ///   - `concat( V, W | c)  =>  concat(V,W) | c`
        public override void getOpList(List<uint> oplist)
        {
            oplist.Add(CPUI_PIECE);
        }

        public override int applyOp(PcodeOp op, Funcdata data)
        {
            Varnode vn;
            Varnode hi;
            Varnode lo;
            Varnode newvn;
            PcodeOp logicop;
            PcodeOp newconcat;
            OpCode opc;
            ulong val;

            int outsz = op.getOut().getSize();
            if (outsz > sizeof(ulong))
                return 0;           // FIXME:  precision problem for constants
            for (int i = 0; i < 2; ++i)
            {
                vn = op.getIn(i);
                if (!vn.isWritten()) continue;
                logicop = vn.getDef();
                opc = logicop.code();
                if ((opc == CPUI_INT_OR) || (opc == CPUI_INT_XOR))
                {
                    if (!logicop.getIn(1).isConstant()) continue;
                    val = logicop.getIn(1).getOffset();
                    if (i == 0)
                    {
                        hi = logicop.getIn(0);
                        lo = op.getIn(1);
                        val <<= 8 * lo.getSize();
                    }
                    else
                    {
                        hi = op.getIn(0);
                        lo = logicop.getIn(0);
                    }
                }
                else if (opc == CPUI_INT_AND)
                {
                    if (!logicop.getIn(1).isConstant()) continue;
                    val = logicop.getIn(1).getOffset();
                    if (i == 0)
                    {
                        hi = logicop.getIn(0);
                        lo = op.getIn(1);
                        val <<= 8 * lo.getSize();
                        val |= Globals.calc_mask(lo.getSize());
                    }
                    else
                    {
                        hi = op.getIn(0);
                        lo = logicop.getIn(0);
                        val |= (calc_mask(hi.getSize()) << 8 * lo.getSize());
                    }
                }
                else
                    continue;
                if (hi.isFree()) continue;
                if (lo.isFree()) continue;
                newconcat = data.newOp(2, op.getAddr());
                data.opSetOpcode(newconcat, CPUI_PIECE);
                newvn = data.newUniqueOut(outsz, newconcat);
                data.opSetInput(newconcat, hi, 0);
                data.opSetInput(newconcat, lo, 1);
                data.opInsertBefore(newconcat, op);
                data.opSetOpcode(op, opc);
                data.opSetInput(op, newvn, 0);
                data.opSetInput(op, data.newConstant(newvn.getSize(), val), 1);
                return 1;
            }
            return 0;
        }
    }
}
