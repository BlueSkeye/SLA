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
    internal class RuleSignShift : Rule
    {
        public RuleSignShift(string g)
            : base(g, 0, "signshift")
        {
        }

        public override Rule? clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule)null;
            return new RuleSignShift(getGroup());
        }

        /// \class RuleSignShift
        /// \brief Normalize sign-bit extraction:  `V >> 0x1f   =>  (V s>> 0x1f) * -1`
        ///
        /// A logical shift of the sign-bit gets converted to an arithmetic shift if it is involved
        /// in an arithmetic expression or a comparison.
        public override void getOpList(List<OpCode> oplist)
        {
            oplist.Add(OpCode.CPUI_INT_RIGHT);
        }

        public override int applyOp(PcodeOp op, Funcdata data)
        {
            Varnode constVn = op.getIn(1);
            if (!constVn.isConstant()) return false;
            ulong val = constVn.getOffset();
            Varnode inVn = op.getIn(0);
            if (val != 8 * inVn.getSize() - 1) return false;
            if (inVn.isFree()) return false;

            bool doConversion = false;
            Varnode outVn = op.getOut();
            IEnumerator<PcodeOp> iter = outVn.beginDescend();
            while (iter.MoveNext()) {
                PcodeOp arithOp = iter.Current;
                switch (arithOp.code()) {
                    case OpCode.CPUI_INT_EQUAL:
                    case OpCode.CPUI_INT_NOTEQUAL:
                        if (arithOp.getIn(1).isConstant())
                            doConversion = true;
                        break;
                    case OpCode.CPUI_INT_ADD:
                    case OpCode.CPUI_INT_MULT:
                        doConversion = true;
                        break;
                    default:
                        break;
                }
                if (doConversion)
                    break;
            }
            if (!doConversion)
                return false;
            PcodeOp shiftOp = data.newOp(2, op.getAddr());
            data.opSetOpcode(shiftOp, OpCode.CPUI_INT_SRIGHT);
            Varnode uniqueVn = data.newUniqueOut(inVn.getSize(), shiftOp);
            data.opSetInput(op, uniqueVn, 0);
            data.opSetInput(op, data.newConstant(inVn.getSize(), Globals.calc_mask((uint)inVn.getSize())), 1);
            data.opSetOpcode(op, OpCode.CPUI_INT_MULT);
            data.opSetInput(shiftOp, inVn, 0);
            data.opSetInput(shiftOp, constVn, 1);
            data.opInsertBefore(shiftOp, op);
            return true;
        }
    }
}
