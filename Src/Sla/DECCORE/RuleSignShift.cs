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
    internal class RuleSignShift : Rule
    {
        public RuleSignShift(string g)
            : base(g, 0, "signshift")
        {
        }

        public override Rule clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule*)0;
            return new RuleSignShift(getGroup());
        }

        /// \class RuleSignShift
        /// \brief Normalize sign-bit extraction:  `V >> 0x1f   =>  (V s>> 0x1f) * -1`
        ///
        /// A logical shift of the sign-bit gets converted to an arithmetic shift if it is involved
        /// in an arithmetic expression or a comparison.
        public override void getOpList(List<uint> oplist)
        {
            oplist.Add(CPUI_INT_RIGHT);
        }

        public override int applyOp(PcodeOp op, Funcdata data)
        {
            ulong val;
            Varnode* constVn = op.getIn(1);
            if (!constVn.isConstant()) return 0;
            val = constVn.getOffset();
            Varnode* inVn = op.getIn(0);
            if (val != 8 * inVn.getSize() - 1) return 0;
            if (inVn.isFree()) return 0;

            bool doConversion = false;
            Varnode* outVn = op.getOut();
            list<PcodeOp*>::const_iterator iter = outVn.beginDescend();
            while (iter != outVn.endDescend())
            {
                PcodeOp* arithOp = *iter;
                ++iter;
                switch (arithOp.code())
                {
                    case CPUI_INT_EQUAL:
                    case CPUI_INT_NOTEQUAL:
                        if (arithOp.getIn(1).isConstant())
                            doConversion = true;
                        break;
                    case CPUI_INT_ADD:
                    case CPUI_INT_MULT:
                        doConversion = true;
                        break;
                    default:
                        break;
                }
                if (doConversion)
                    break;
            }
            if (!doConversion)
                return 0;
            PcodeOp* shiftOp = data.newOp(2, op.getAddr());
            data.opSetOpcode(shiftOp, CPUI_INT_SRIGHT);
            Varnode* uniqueVn = data.newUniqueOut(inVn.getSize(), shiftOp);
            data.opSetInput(op, uniqueVn, 0);
            data.opSetInput(op, data.newConstant(inVn.getSize(), Globals.calc_mask(inVn.getSize())), 1);
            data.opSetOpcode(op, CPUI_INT_MULT);
            data.opSetInput(shiftOp, inVn, 0);
            data.opSetInput(shiftOp, constVn, 1);
            data.opInsertBefore(shiftOp, op);
            return 1;
        }
    }
}
