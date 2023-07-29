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
    internal class RuleSubvarShift : Rule
    {
        public RuleSubvarShift(string g)
            : base(g, 0, "subvar_shift")
        {
        }

        public override Rule clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule*)0;
            return new RuleSubvarShift(getGroup());
        }

        /// \class RuleSubvarShift
        /// \brief Perform SubvariableFlow analysis triggered by INT_RIGHT
        ///
        /// If the INT_RIGHT input has only 1 bit that can possibly be non-zero
        /// and it is getting shifted into the least significant bit position,
        /// trigger the full SubvariableFlow analysis.
        public override void getOpList(List<uint4> oplist)
        {
            oplist.push_back(CPUI_INT_RIGHT);
        }

        public override int4 applyOp(PcodeOp op, Funcdata data)
        {
            Varnode* vn = op.getIn(0);
            if (vn.getSize() != 1) return 0;
            if (!op.getIn(1).isConstant()) return 0;
            int4 sa = (int4)op.getIn(1).getOffset();
            uintb mask = vn.getNZMask();
            if ((mask >> sa) != (uintb)1) return 0; // Pulling out a single bit
            mask = (mask >> sa) << sa;
            if (op.getOut().hasNoDescend()) return 0;

            SubvariableFlow subflow(&data,vn,mask,false,false,false);
            if (!subflow.doTrace()) return 0;
            subflow.doReplacement();
            return 1;
        }
    }
}
