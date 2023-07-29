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
    internal class RuleSub2Add : Rule
    {
        public RuleSub2Add(string g)
            : base(g, 0, "sub2add")
        {
        }

        public override Rule clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule*)0;
            return new RuleSub2Add(getGroup());
        }

        /// \class RuleSub2Add
        /// \brief Eliminate INT_SUB:  `V - W  =>  V + W * -1`
        public override void getOpList(List<uint> oplist)
        {
            oplist.push_back(CPUI_INT_SUB);
        }

        public override int applyOp(PcodeOp op, Funcdata data)
        {
            PcodeOp* newop;
            Varnode* vn,*newvn;

            vn = op.getIn(1);      // Parameter being subtracted
            newop = data.newOp(2, op.getAddr());
            data.opSetOpcode(newop, CPUI_INT_MULT);
            newvn = data.newUniqueOut(vn.getSize(), newop);
            data.opSetInput(op, newvn, 1); // Replace vn's reference first
            data.opSetInput(newop, vn, 0);
            data.opSetInput(newop, data.newConstant(vn.getSize(), calc_mask(vn.getSize())), 1);
            data.opSetOpcode(op, CPUI_INT_ADD);
            data.opInsertBefore(newop, op);
            return 1;
        }
    }
}
