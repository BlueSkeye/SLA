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
    internal class RuleAndDistribute : Rule
    {
        public RuleAndDistribute(string g)
            : base(g, 0, "anddistribute")
        {
        }

        public override Rule? clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule)null;
            return new RuleAndDistribute(getGroup());
        }

        /// \class RuleAndDistribute
        /// \brief Distribute INT_AND through INT_OR if result is simpler
        public override void getOpList(List<OpCode> oplist)
        {
            oplist.Add(OpCode.CPUI_INT_AND);
        }

        public override bool applyOp(PcodeOp op, Funcdata data)
        {
            Varnode orvn;
            Varnode othervn;
            Varnode newvn1;
            Varnode newvn2;
            PcodeOp? orop = (PcodeOp)null;
            PcodeOp newop1, newop2;
            ulong ormask1, ormask2, othermask, fullmask;
            int i, size;

            size = op.getOut().getSize();
            if (size > sizeof(ulong)) return 0; // FIXME: ulong should be arbitrary precision
            fullmask = Globals.calc_mask((uint)size);
            for (i = 0; i < 2; ++i) {
                othervn = op.getIn(1 - i);
                if (!othervn.isHeritageKnown()) continue;
                orvn = op.getIn(i);
                orop = orvn.getDef();
                if (orop == (PcodeOp)null) continue;
                if (orop.code() != OpCode.CPUI_INT_OR) continue;
                if (!orop.getIn(0).isHeritageKnown()) continue;
                if (!orop.getIn(1).isHeritageKnown()) continue;
                othermask = othervn.getNZMask();
                if (othermask == 0) continue; // This case picked up by andmask
                if (othermask == fullmask) continue; // Nothing useful from distributing
                ormask1 = orop.getIn(0).getNZMask();
                if ((ormask1 & othermask) == 0) break; // AND would cancel if distributed
                ormask2 = orop.getIn(1).getNZMask();
                if ((ormask2 & othermask) == 0) break; // AND would cancel if distributed
                if (othervn.isConstant()) {
                    if ((ormask1 & othermask) == ormask1) break; // AND is trivial if distributed
                    if ((ormask2 & othermask) == ormask2) break;
                }
            }
            if (i == 2) return 0;
            // Do distribution
            newop1 = data.newOp(2, op.getAddr()); // Distribute AND
            newvn1 = data.newUniqueOut(size, newop1);
            data.opSetOpcode(newop1, OpCode.CPUI_INT_AND);
            data.opSetInput(newop1, orop.getIn(0), 0); // To first input of original OR
            data.opSetInput(newop1, othervn, 1);
            data.opInsertBefore(newop1, op);

            newop2 = data.newOp(2, op.getAddr()); // Distribute AND
            newvn2 = data.newUniqueOut(size, newop2);
            data.opSetOpcode(newop2, OpCode.CPUI_INT_AND);
            data.opSetInput(newop2, orop.getIn(1), 0); // To second input of original OR
            data.opSetInput(newop2, othervn, 1);
            data.opInsertBefore(newop2, op);

            data.opSetInput(op, newvn1, 0); // new OR's inputs are outputs of new ANDs
            data.opSetInput(op, newvn2, 1);
            data.opSetOpcode(op, OpCode.CPUI_INT_OR);

            return 1;
        }
    }
}
