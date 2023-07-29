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
    internal class RuleZextCommute : Rule
    {
        public RuleZextCommute(string g)
            : base(g, 0, "zextcommute")
        {
        }

        public override Rule clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule*)0;
            return new RuleZextCommute(getGroup());
        }

        /// \class RuleZextCommute
        /// \brief Commute INT_ZEXT with INT_RIGHT: `zext(V) >> W  =>  zext(V >> W)`
        public override void getOpList(List<uint4> oplist)
        {
            oplist.push_back(CPUI_INT_RIGHT);
        }

        public override int4 applyOp(PcodeOp op, Funcdata data)
        {
            Varnode* zextvn = op.getIn(0);
            if (!zextvn.isWritten()) return 0;
            PcodeOp* zextop = zextvn.getDef();
            if (zextop.code() != CPUI_INT_ZEXT) return 0;
            Varnode* zextin = zextop.getIn(0);
            if (zextin.isFree()) return 0;
            Varnode* savn = op.getIn(1);
            if ((!savn.isConstant()) && (savn.isFree()))
                return 0;

            PcodeOp* newop = data.newOp(2, op.getAddr());
            data.opSetOpcode(newop, CPUI_INT_RIGHT);
            Varnode* newout = data.newUniqueOut(zextin.getSize(), newop);
            data.opRemoveInput(op, 1);
            data.opSetInput(op, newout, 0);
            data.opSetOpcode(op, CPUI_INT_ZEXT);
            data.opSetInput(newop, zextin, 0);
            data.opSetInput(newop, savn, 1);
            data.opInsertBefore(newop, op);
            return 1;
        }
    }
}
