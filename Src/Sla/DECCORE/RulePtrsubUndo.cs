using ghidra;
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
    internal class RulePtrsubUndo : Rule
    {
        public RulePtrsubUndo(string g)
            : base(g, 0, "ptrsubundo")
        {
        }

        public override Rule clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule*)0;
            return new RulePtrsubUndo(getGroup());
        }

        /// \class RulePtrsubUndo
        /// \brief Remove PTRSUB operations with mismatched data-type information
        ///
        /// Incorrect data-types may be assigned to Varnodes in the middle of simplification. This causes
        /// incorrect PTRSUBs, which are discovered later. This rule converts the PTRSUB back to an INT_ADD
        /// when the mistake is discovered.
        public override void getOpList(List<uint> oplist)
        {
            oplist.Add(CPUI_PTRSUB);
        }

        public override int applyOp(PcodeOp op, Funcdata data)
        {
            if (!data.hasTypeRecoveryStarted()) return 0;

            Varnode* basevn = op.getIn(0);
            if (basevn.getTypeReadFacing(op).isPtrsubMatching(op.getIn(1).getOffset()))
                return 0;

            data.opSetOpcode(op, OpCode.CPUI_INT_ADD);
            op.clearStopTypePropagation();
            return 1;
        }
    }
}
