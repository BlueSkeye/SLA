using Sla.CORE;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Sla.DECCORE
{
    internal class RuleBitUndistribute : Rule
    {
        public RuleBitUndistribute(string g)
            : base(g, 0, "bitundistribute")
        {
        }

        public override Rule clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule)null;
            return new RuleBitUndistribute(getGroup());
        }

        /// \class RuleBitUndistribute
        /// \brief Undo distributed operations through INT_AND, INT_OR, and INT_XOR
        ///
        ///  - `zext(V) & zext(W)  =>  zext( V & W )`
        ///  - `(V >> X) | (W >> X)  =>  (V | W) >> X`
        ///
        /// Works with INT_ZEXT, INT_SEXT, INT_LEFT, INT_RIGHT, and INT_SRIGHT.
        public override void getOpList(List<OpCode> oplist)
        {
            uint list[] = { OpCode.CPUI_INT_AND, OpCode.CPUI_INT_OR, OpCode.CPUI_INT_XOR };
            oplist.insert(oplist.end(), list, list + 3);
        }

        public override int applyOp(PcodeOp op, Funcdata data)
        {
            Varnode vn1 = op.getIn(0);
            Varnode vn2 = op.getIn(1);
            Varnode in1;
            Varnode in2;
            Varnode vnextra;
            OpCode opc;

            if (!vn1.isWritten()) return 0;
            if (!vn2.isWritten()) return 0;

            opc = vn1.getDef().code();
            if (vn2.getDef().code() != opc) return 0;
            switch (opc)
            {
                case OpCode.CPUI_INT_ZEXT:
                case OpCode.CPUI_INT_SEXT:
                    // Test for full equality of extension operation
                    in1 = vn1.getDef().getIn(0);
                    if (in1.isFree()) return 0;
                    in2 = vn2.getDef().getIn(0);
                    if (in2.isFree()) return 0;
                    if (in1.getSize() != in2.getSize()) return 0;
                    data.opRemoveInput(op, 1);
                    break;
                case OpCode.CPUI_INT_LEFT:
                case OpCode.CPUI_INT_RIGHT:
                case OpCode.CPUI_INT_SRIGHT:
                    // Test for full equality of shift operation
                    in1 = vn1.getDef().getIn(1);
                    in2 = vn2.getDef().getIn(1);
                    if (in1.isConstant() && in2.isConstant())
                    {
                        if (in1.getOffset() != in2.getOffset())
                            return 0;
                        vnextra = data.newConstant(in1.getSize(), in1.getOffset());
                    }
                    else if (in1 != in2)
                        return 0;
                    else
                    {
                        if (in1.isFree()) return 0;
                        vnextra = in1;
                    }
                    in1 = vn1.getDef().getIn(0);
                    if (in1.isFree()) return 0;
                    in2 = vn2.getDef().getIn(0);
                    if (in2.isFree()) return 0;
                    data.opSetInput(op, vnextra, 1);
                    break;
                default:
                    return 0;
            }

            PcodeOp* newext = data.newOp(2, op.getAddr());
            Varnode* smalllogic = data.newUniqueOut(in1.getSize(), newext);
            data.opSetInput(newext, in1, 0);
            data.opSetInput(newext, in2, 1);
            data.opSetOpcode(newext, op.code());

            data.opSetOpcode(op, opc);
            data.opSetInput(op, smalllogic, 0);
            data.opInsertBefore(newext, op);
            return 1;
        }
    }
}
