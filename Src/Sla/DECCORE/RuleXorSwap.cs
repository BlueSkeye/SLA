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
    internal class RuleXorSwap : Rule
    {
        public RuleXorSwap(string g)
            : base(g,0,"xorswap")
        {
        }

        public override Rule clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule*)0;
            return new RuleXorSwap(getGroup());
        }

        /// \class RuleXorSwap
        /// \brief Simplify limited chains of XOR operations
        ///
        /// `V = (a ^ b) ^ a => V = b`
        /// `V = a ^ (b ^ a) => V = b`
        public override void getOpList(List<uint> oplist)
        {
            oplist.Add(CPUI_INT_XOR);
        }

        public override int applyOp(PcodeOp op, Funcdata data)
        {
            for (int i = 0; i < 2; ++i)
            {
                Varnode* vn = op.getIn(i);
                if (!vn.isWritten()) continue;
                PcodeOp* op2 = vn.getDef();
                if (op2.code() != OpCode.CPUI_INT_XOR) continue;
                Varnode* othervn = op.getIn(1 - i);
                Varnode* vn0 = op2.getIn(0);
                Varnode* vn1 = op2.getIn(1);
                if (othervn == vn0 && !vn1.isFree())
                {
                    data.opRemoveInput(op, 1);
                    data.opSetOpcode(op, OpCode.CPUI_COPY);
                    data.opSetInput(op, vn1, 0);
                    return 1;
                }
                else if (othervn == vn1 && !vn0.isFree())
                {
                    data.opRemoveInput(op, 1);
                    data.opSetOpcode(op, OpCode.CPUI_COPY);
                    data.opSetInput(op, vn0, 0);
                    return 1;
                }
            }
            return 0;
        }
    }
}
