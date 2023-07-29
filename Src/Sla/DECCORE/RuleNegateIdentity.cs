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
    internal class RuleNegateIdentity : Rule
    {
        public RuleNegateIdentity(string g)
            : base(g, 0, "negateidentity")
        {
        }

        public override Rule clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule*)0;
            return new RuleNegateIdentity(getGroup());
        }

        /// \class RuleNegateIdentity
        /// \brief Apply INT_NEGATE identities:  `V & ~V  => #0,  V | ~V  .  #-1`
        public override void getOpList(List<uint4> oplist)
        {
            oplist.push_back(CPUI_INT_NEGATE);
        }

        public override int4 applyOp(PcodeOp op, Funcdata data)
        {
            Varnode* vn = op.getIn(0);
            Varnode* outVn = op.getOut();
            list<PcodeOp*>::const_iterator iter;
            for (iter = outVn.beginDescend(); iter != outVn.endDescend(); ++iter)
            {
                PcodeOp* logicOp = *iter;
                OpCode opc = logicOp.code();
                if (opc != CPUI_INT_AND && opc != CPUI_INT_OR && opc != CPUI_INT_XOR)
                    continue;
                int4 slot = logicOp.getSlot(outVn);
                if (logicOp.getIn(1 - slot) != vn) continue;
                uintb value = 0;
                if (opc != CPUI_INT_AND)
                    value = calc_mask(vn.getSize());
                data.opSetInput(logicOp, data.newConstant(vn.getSize(), value), 0);
                data.opRemoveInput(logicOp, 1);
                data.opSetOpcode(logicOp, CPUI_COPY);
                return 1;
            }
            return 0;
        }
    }
}
