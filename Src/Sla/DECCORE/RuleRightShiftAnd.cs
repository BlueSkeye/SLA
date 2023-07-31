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
    internal class RuleRightShiftAnd : Rule
    {
        public RuleRightShiftAnd(string g)
            : base(g, 0, "rightshiftand")
        {
        }

        public override Rule clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule*)0;
            return new RuleRightShiftAnd(getGroup());
        }

        /// \class RuleRightShiftAnd
        /// \brief Simplify INT_RIGHT and INT_SRIGHT ops where an INT_AND mask becomes unnecessary
        ///
        /// - `( V & 0xf000 ) >> 24   =>   V >> 24`
        /// - `( V & 0xf000 ) s>> 24  =>   V s>> 24`
        public override void getOpList(List<uint> oplist)
        {
            oplist.Add(CPUI_INT_RIGHT);
            oplist.Add(CPUI_INT_SRIGHT);
        }

        public override int applyOp(PcodeOp op, Funcdata data)
        {
            Varnode* constVn = op.getIn(1);
            if (!constVn.isConstant()) return 0;
            Varnode* inVn = op.getIn(0);
            if (!inVn.isWritten()) return 0;
            PcodeOp* andOp = inVn.getDef();
            if (andOp.code() != OpCode.CPUI_INT_AND) return 0;
            Varnode* maskVn = andOp.getIn(1);
            if (!maskVn.isConstant()) return 0;

            int sa = (int)constVn.getOffset();
            ulong mask = maskVn.getOffset() >> sa;
            Varnode* rootVn = andOp.getIn(0);
            ulong full = Globals.calc_mask(rootVn.getSize()) >> sa;
            if (full != mask) return 0;
            if (rootVn.isFree()) return 0;
            data.opSetInput(op, rootVn, 0); // Bypass the INT_AND
            return 1;
        }
    }
}
