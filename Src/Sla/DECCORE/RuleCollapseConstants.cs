using Sla.CORE;

namespace Sla.DECCORE
{
    internal class RuleCollapseConstants : Rule
    {
        public RuleCollapseConstants(string g)
            : base(g, 0, "collapseconstants")
        {
        }

        public override Rule? clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule)null;
            return new RuleCollapseConstants(getGroup());
        }

        // applies to all opcodes
        /// \class RuleCollapseConstants
        /// \brief Collapse constant expressions
        public override int applyOp(PcodeOp op, Funcdata data)
        {
            if (!op.isCollapsible()) {
                // Expression must be collapsible
                return 0;
            }

            Address newval;
            bool markedInput = false;
            try {
                newval = data.getArch().getConstant(op.collapse(markedInput));
            }
            catch (LowlevelError) {
                // Dont know how or dont want to collapse further
                data.opMarkNoCollapse(op);
                return 0;
            }

            // Create new collapsed constant
            Varnode vn = data.newVarnode(op.getOut().getSize(), newval);
            if (markedInput) {
                op.collapseConstantSymbol(vn);
            }
            for (int i = op.numInput() - 1; i > 0; --i) {
                // unlink old constants
                data.opRemoveInput(op, i);
            }
            // Link in new collapsed constant
            data.opSetInput(op, vn, 0);
            // Change ourselves to a copy
            data.opSetOpcode(op, OpCode.CPUI_COPY);
            return 1;
        }
    }
}
