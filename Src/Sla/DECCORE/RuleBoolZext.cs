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
    internal class RuleBoolZext : Rule
    {
        public RuleBoolZext(string g)
            : base(g, 0, "boolzext")
        {
        }

        public override Rule clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule)null;
            return new RuleBoolZext(getGroup());
        }

        /// \class RuleBoolZext
        /// \brief Simplify boolean expressions of the form zext(V) * -1
        ///
        ///   - `(zext(V) * -1) + 1  =>  zext( !V )`
        ///   - `(zext(V) * -1) == -1  =>  V == true`
        ///   - `(zext(V) * -1) != -1  =>  V != true`
        ///   - `(zext(V) * -1) & (zext(W) * -1)  =>  zext(V && W) * -1`
        ///   - `(zext(V) * -1) | (zext(W) * -1)  =>  zext(V || W) * -1`
        public override void getOpList(List<OpCode> oplist)
        {
            oplist.Add(CPUI_INT_ZEXT);
        }

        public override int applyOp(PcodeOp op, Funcdata data)
        {
            Varnode* boolVn1,*boolVn2;
            PcodeOp* multop1,*actionop;
            PcodeOp* zextop2,*multop2;
            ulong coeff, val;
            OpCode opc;
            int size;

            boolVn1 = op.getIn(0);
            if (!boolVn1.isBooleanValue(data.isTypeRecoveryOn())) return 0;

            multop1 = op.getOut().loneDescend();
            if (multop1 == (PcodeOp)null) return 0;
            if (multop1.code() != OpCode.CPUI_INT_MULT) return 0;
            if (!multop1.getIn(1).isConstant()) return 0;
            coeff = multop1.getIn(1).getOffset();
            if (coeff != Globals.calc_mask(multop1.getIn(1).getSize()))
                return 0;
            size = multop1.getOut().getSize();

            // If we reached here, we are Multiplying extended boolean by -1
            actionop = multop1.getOut().loneDescend();
            if (actionop == (PcodeOp)null) return 0;
            switch (actionop.code())
            {
                case OpCode.CPUI_INT_ADD:
                    if (!actionop.getIn(1).isConstant()) return 0;
                    if (actionop.getIn(1).getOffset() == 1)
                    {
                        Varnode* vn;
                        PcodeOp* newop = data.newOp(1, op.getAddr());
                        data.opSetOpcode(newop, OpCode.CPUI_BOOL_NEGATE);  // Negate the boolean
                        vn = data.newUniqueOut(1, newop);
                        data.opSetInput(newop, boolVn1, 0);
                        data.opInsertBefore(newop, op);
                        data.opSetInput(op, vn, 0);
                        data.opRemoveInput(actionop, 1); // eliminate the INT_ADD operator
                        data.opSetOpcode(actionop, OpCode.CPUI_COPY);
                        data.opSetInput(actionop, op.getOut(), 0); // propagate past the INT_MULT operator
                        return 1;
                    }
                    return 0;
                case OpCode.CPUI_INT_EQUAL:
                case OpCode.CPUI_INT_NOTEQUAL:

                    if (actionop.getIn(1).isConstant())
                    {
                        val = actionop.getIn(1).getOffset();
                    }
                    else
                        return 0;

                    // Change comparison of extended boolean to 0 or -1
                    // to comparison of unextended boolean to 0 or 1
                    if (val == coeff)
                        val = 1;
                    else if (val != 0)
                        return 0;           // Not comparing with 0 or -1

                    data.opSetInput(actionop, boolVn1, 0);
                    data.opSetInput(actionop, data.newConstant(1, val), 1);
                    return 1;
                case OpCode.CPUI_INT_AND:
                    opc = OpCode.CPUI_BOOL_AND;
                    break;
                case OpCode.CPUI_INT_OR:
                    opc = OpCode.CPUI_BOOL_OR;
                    break;
                case OpCode.CPUI_INT_XOR:
                    opc = OpCode.CPUI_BOOL_XOR;
                    break;
                default:
                    return 0;
            }

            // Apparently doing logical ops with extended boolean

            // Check that the other side is also an extended boolean
            multop2 = (multop1 == actionop.getIn(0).getDef()) ? actionop.getIn(1).getDef() : actionop.getIn(0).getDef();
            if (multop2 == (PcodeOp)null) return 0;
            if (multop2.code() != OpCode.CPUI_INT_MULT) return 0;
            if (!multop2.getIn(1).isConstant()) return 0;
            coeff = multop2.getIn(1).getOffset();
            if (coeff != Globals.calc_mask(size))
                return 0;
            zextop2 = multop2.getIn(0).getDef();
            if (zextop2 == (PcodeOp)null) return 0;
            if (zextop2.code() != OpCode.CPUI_INT_ZEXT) return 0;
            boolVn2 = zextop2.getIn(0);
            if (!boolVn2.isBooleanValue(data.isTypeRecoveryOn())) return 0;

            // Do the boolean calculation on unextended boolean values
            // and then extend the result
            PcodeOp* newop = data.newOp(2, actionop.getAddr());
            Varnode* newres = data.newUniqueOut(1, newop);
            data.opSetOpcode(newop, opc);
            data.opSetInput(newop, boolVn1, 0);
            data.opSetInput(newop, boolVn2, 1);
            data.opInsertBefore(newop, actionop);

            PcodeOp* newzext = data.newOp(1, actionop.getAddr());
            Varnode* newzout = data.newUniqueOut(size, newzext);
            data.opSetOpcode(newzext, OpCode.CPUI_INT_ZEXT);
            data.opSetInput(newzext, newres, 0);
            data.opInsertBefore(newzext, actionop);

            data.opSetOpcode(actionop, OpCode.CPUI_INT_MULT);
            data.opSetInput(actionop, newzout, 0);
            data.opSetInput(actionop, data.newConstant(size, coeff), 1);
            return 1;
        }
    }
}
