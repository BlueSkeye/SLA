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
    internal class RulePullsubIndirect : Rule
    {
        public RulePullsubIndirect(string g)
            : base(g, 0, "pullsub_indirect")
        {
        }

        public override Rule clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule)null;
            return new RulePullsubIndirect(getGroup());
        }

        /// \class RulePullsubIndirect
        /// \brief Pull-back SUBPIECE through INDIRECT
        public override void getOpList(List<OpCode> oplist)
        {
            oplist.Add(CPUI_SUBPIECE);
        }

        public override int applyOp(PcodeOp op, Funcdata data)
        {
            int maxByte, minByte, newSize;

            Varnode* vn = op.getIn(0);
            if (!vn.isWritten()) return 0;
            PcodeOp* indir = vn.getDef();
            if (indir.code() != OpCode.CPUI_INDIRECT) return 0;
            if (indir.getIn(1).getSpace().getType() != spacetype.IPTR_IOP) return 0;

            PcodeOp* targ_op = PcodeOp::getOpFromConst(indir.getIn(1).getAddr());
            if (targ_op.isDead()) return 0;
            if (vn.isAddrForce()) return 0;
            RulePullsubMulti::minMaxUse(vn, maxByte, minByte);
            newSize = maxByte - minByte + 1;
            if (maxByte < minByte || (newSize >= vn.getSize()))
                return 0;
            if (!RulePullsubMulti::acceptableSize(newSize)) return 0;
            Varnode* outvn = op.getOut();
            if (outvn.isPrecisLo() || outvn.isPrecisHi()) return 0; // Don't pull apart double precision object

            ulong consume = Globals.calc_mask(newSize) << 8 * minByte;
            consume = ~consume;
            if ((consume & indir.getIn(0).getConsume()) != 0) return 0;

            Varnode* small2;
            Address smalladdr2;
            PcodeOp* new_ind;

            if (!vn.getSpace().isBigEndian())
                smalladdr2 = vn.getAddr() + minByte;
            else
                smalladdr2 = vn.getAddr() + (vn.getSize() - maxByte - 1);

            if (indir.isIndirectCreation())
            {
                bool possibleout = !indir.getIn(0).isIndirectZero();
                new_ind = data.newIndirectCreation(targ_op, smalladdr2, newSize, possibleout);
                small2 = new_ind.getOut();
            }
            else
            {
                Varnode* basevn = indir.getIn(0);
                Varnode* small1 = RulePullsubMulti::findSubpiece(basevn, newSize, op.getIn(1).getOffset());
                if (small1 == (Varnode)null)
                    small1 = RulePullsubMulti::buildSubpiece(basevn, newSize, op.getIn(1).getOffset(), data);
                // Create new indirect near original indirect
                new_ind = data.newOp(2, indir.getAddr());
                data.opSetOpcode(new_ind, OpCode.CPUI_INDIRECT);
                small2 = data.newVarnodeOut(newSize, smalladdr2, new_ind);
                data.opSetInput(new_ind, small1, 0);
                data.opSetInput(new_ind, data.newVarnodeIop(targ_op), 1);
                data.opInsertBefore(new_ind, indir);
            }

            RulePullsubMulti::replaceDescendants(vn, small2, maxByte, minByte, data);
            return 1;
        }
    }
}
