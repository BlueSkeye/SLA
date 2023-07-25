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
    internal class RuleOrMask : Rule
    {
        public RuleOrMask(string g)
            : base(g, 0, "ormask")
        {
        }


        public override Rule clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule*)0;
            return new RuleOrMask(getGroup());
        }

        /// \class RuleOrMask
        /// \brief Simplify INT_OR with full mask:  `V = W | 0xffff  =>  V = W`
        public override void getOpList(List<uint4> oplist)
        {
            oplist.push_back(CPUI_INT_OR);
        }

        public override int4 applyOp(PcodeOp op, Funcdata data)
        {
            int4 size = op->getOut()->getSize();
            if (size > sizeof(uintb)) return 0; // FIXME: uintb should be arbitrary precision
            Varnode* constvn;

            constvn = op->getIn(1);
            if (!constvn->isConstant()) return 0;
            uintb val = constvn->getOffset();
            uintb mask = calc_mask(size);
            if ((val & mask) != mask) return 0;
            data.opSetOpcode(op, CPUI_COPY);
            data.opSetInput(op, constvn, 0);
            data.opRemoveInput(op, 1);
            return 1;
        }
    }
}
