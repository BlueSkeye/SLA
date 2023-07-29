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
    internal class RuleCondNegate : Rule
    {
        public RuleCondNegate(string g)
            : base(g, 0, "condnegate")
        {
        }

        public override Rule clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule*)0;
            return new RuleCondNegate(getGroup());
        }

        /// \class RuleCondNegate
        /// \brief Flip conditions to match structuring cues
        ///
        /// Structuring control-flow introduces a preferred meaning to individual
        /// branch directions as \b true or \b false, but this may conflict with the
        /// natural meaning of the boolean calculation feeding into a CBRANCH.
        /// This Rule introduces a BOOL_NEGATE op as necessary to get the meanings to align.
        public override void getOpList(List<uint> oplist)
        {
            oplist.Add(CPUI_CBRANCH);
        }

        public override int applyOp(PcodeOp op, Funcdata data)
        {
            PcodeOp* newop;
            Varnode* vn,*outvn;

            if (!op.isBooleanFlip()) return 0;

            vn = op.getIn(1);
            newop = data.newOp(1, op.getAddr());
            data.opSetOpcode(newop, CPUI_BOOL_NEGATE);
            outvn = data.newUniqueOut(1, newop); // Flipped version of varnode
            data.opSetInput(newop, vn, 0);
            data.opSetInput(op, outvn, 1);
            data.opInsertBefore(newop, op);
            data.opFlipCondition(op);   // Flip meaning of condition
                                        // NOTE fallthru block is still same status
            return 1;
        }
    }
}
