using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Sla.DECCORE
{
    /// \brief Define formal link between stack-pointer values before and after sub-function calls.
    ///
    /// Change to the stack-pointer across a sub-function is called \b extrapop. This class
    /// makes sure there is p-code relationship between the Varnode coming into a sub-function
    /// and the Varnode coming out.  If the \e extrapop is known, the p-code will be
    /// a CPUI_COPY or CPUI_ADD. If it is unknown, a CPUI_INDIRECT will be inserted that gets
    /// filled in by ActionStackPtrFlow.
    internal class ActionExtraPopSetup : Action
    {
        private AddrSpace stackspace;      ///< The stack space to analyze
        
        public ActionExtraPopSetup(string g,AddrSpace ss)
            : base(rule_onceperfunc,"extrapopsetup", g)
        {
            stackspace = ss;
        }

        public override Action? clone(ActionGroupList grouplist)
        {
            return (!grouplist.contains(getGroup())) ? null : new ActionExtraPopSetup(getGroup(), stackspace);
        }

        public override int apply(Funcdata data)
        {
            FuncCallSpecs* fc;
            PcodeOp* op;

            if (stackspace == (AddrSpace)null) return 0; // No stack to speak of
            VarnodeData point = stackspace.getSpacebase(0);
            Address sb_addr(point.space, point.offset);
            int sb_size = point.size;

            for (int i = 0; i < data.numCalls(); ++i)
            {
                fc = data.getCallSpecs(i);
                if (fc.getExtraPop() == 0) continue; // Stack pointer is undisturbed
                op = data.newOp(2, fc.getOp().getAddr());
                data.newVarnodeOut(sb_size, sb_addr, op);
                data.opSetInput(op, data.newVarnode(sb_size, sb_addr), 0);
                if (fc.getExtraPop() != ProtoModel::extrapop_unknown)
                { // We know exactly how stack pointer is changed
                    fc.setEffectiveExtraPop(fc.getExtraPop());
                    data.opSetOpcode(op, CPUI_INT_ADD);
                    data.opSetInput(op, data.newConstant(sb_size, fc.getExtraPop()), 1);
                    data.opInsertAfter(op, fc.getOp());
                }
                else
                {           // We don't know exactly, so we create INDIRECT
                    data.opSetOpcode(op, CPUI_INDIRECT);
                    data.opSetInput(op, data.newVarnodeIop(fc.getOp()), 1);
                    data.opInsertBefore(op, fc.getOp());
                }
            }
            return 0;
        }
    }
}
