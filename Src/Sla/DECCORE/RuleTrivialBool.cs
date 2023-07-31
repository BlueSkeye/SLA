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
    internal class RuleTrivialBool : Rule
    {
        public RuleTrivialBool(string g)
            : base(g, 0, "trivialbool")
        {
        }

        public override Rule clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule*)0;
            return new RuleTrivialBool(getGroup());
        }

        /// \class RuleTrivialBool
        /// \brief Simplify boolean expressions when one side is constant
        ///
        ///   - `V && false  =>  false`
        ///   - `V && true   =>  V`
        ///   - `V || false  =>  V`
        ///   - `V || true   =>  true`
        ///   - `V ^^ true   =>  !V`
        ///   - `V ^^ false  =>  V`
        public override void getOpList(List<uint> oplist)
        {
            uint list[] = { OpCode.CPUI_BOOL_AND, OpCode.CPUI_BOOL_OR, OpCode.CPUI_BOOL_XOR };
            oplist.insert(oplist.end(), list, list + 3);
        }

        public override int applyOp(PcodeOp op, Funcdata data)
        {
            Varnode* vnconst = op.getIn(1);
            Varnode* vn;
            ulong val;
            OpCode opc;

            if (!vnconst.isConstant()) return 0;
            val = vnconst.getOffset();

            switch (op.code())
            {
                case OpCode.CPUI_BOOL_XOR:
                    vn = op.getIn(0);
                    opc = (val == 1) ? OpCode.CPUI_BOOL_NEGATE : OpCode.CPUI_COPY;
                    break;
                case OpCode.CPUI_BOOL_AND:
                    opc = OpCode.CPUI_COPY;
                    if (val == 1)
                        vn = op.getIn(0);
                    else
                        vn = data.newConstant(1, 0); // Copy false
                    break;
                case OpCode.CPUI_BOOL_OR:
                    opc = OpCode.CPUI_COPY;
                    if (val == 1)
                        vn = data.newConstant(1, 1);
                    else
                        vn = op.getIn(0);
                    break;
                default:
                    return 0;
            }

            data.opRemoveInput(op, 1);
            data.opSetOpcode(op, opc);
            data.opSetInput(op, vn, 0);
            return 1;
        }
    }
}
