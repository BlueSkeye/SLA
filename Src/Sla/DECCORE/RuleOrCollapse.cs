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
    internal class RuleOrCollapse : Rule
    {
        public RuleOrCollapse(string g)
            : base(g, 0, "orcollapse")
        {
        }

        public override Rule clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule*)0;
            return new RuleOrCollapse(getGroup());
        }

        /// \class RuleOrCollapse
        /// \brief Collapse unnecessary INT_OR
        ///
        /// Replace V | c with c, if any bit not set in c,
        /// is also not set in V   i.e. NZM(V) | c == c
        public override void getOpList(List<uint4> oplist)
        {
            oplist.push_back(CPUI_INT_OR);
        }

        public override int4 applyOp(PcodeOp op, Funcdata data)
        {
            uintb val, mask;
            int4 size = op.getOut().getSize();
            Varnode* vn;

            vn = op.getIn(1);
            if (!vn.isConstant()) return 0;
            if (size > sizeof(uintb)) return 0; // FIXME: uintb should be arbitrary precision
            mask = op.getIn(0).getNZMask();
            val = vn.getOffset();
            if ((mask | val) != val) return 0; // first param may turn on other bits

            data.opSetOpcode(op, CPUI_COPY);
            data.opRemoveInput(op, 0);
            return 1;
        }
    }
}
