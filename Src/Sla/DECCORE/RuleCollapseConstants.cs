using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Sla.DECCORE
{
    internal class RuleCollapseConstants : Rule
    {
        public RuleCollapseConstants(string g)
            : base(g, 0, "collapseconstants")
        {
        }

        public override Rule clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule*)0;
            return new RuleCollapseConstants(getGroup());
        }

        // applies to all opcodes
        /// \class RuleCollapseConstants
        /// \brief Collapse constant expressions
        public override int4 applyOp(PcodeOp op, Funcdata data)
        {
            int4 i;
            Varnode* vn;

            if (!op->isCollapsible()) return 0; // Expression must be collapsible

            Address newval;
            bool markedInput = false;
            try
            {
                newval = data.getArch()->getConstant(op->collapse(markedInput));
            }
            catch (LowlevelError err)
            {
                data.opMarkNoCollapse(op); // Dont know how or dont want to collapse further
                return 0;
            }

            vn = data.newVarnode(op->getOut()->getSize(), newval); // Create new collapsed constant
            if (markedInput)
            {
                op->collapseConstantSymbol(vn);
            }
            for (i = op->numInput() - 1; i > 0; --i)
                data.opRemoveInput(op, i);  // unlink old constants
            data.opSetInput(op, vn, 0); // Link in new collapsed constant
            data.opSetOpcode(op, CPUI_COPY); // Change ourselves to a copy

            return 1;
        }
    }
}
