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
    /// \brief Search for input Varnodes that have been officially provided constant values.
    ///
    /// This class injects p-code at the beginning of the function if there is an official \e uponentry
    /// injection specified for the prototype model or if there are \e tracked registers for which the
    /// user has provided a constant value for.
    internal class ActionConstbase : Action
    {
        /// Constructor
        public ActionConstbase(string g)
            : base(0, "constbase", g)
        {
        }

        public override Action? clone(ActionGroupList grouplist)
        {
            return (!grouplist.contains(getGroup())) ? null : new ActionConstbase(getGroup());
        }

        public override int apply(Funcdata data)
        {
            FuncCallSpecs* fc;
            PcodeOp* op;

            if (stackspace == (AddrSpace*)0) return 0; // No stack to speak of
            const VarnodeData &point(stackspace->getSpacebase(0));
            Address sb_addr(point.space, point.offset);
            int4 sb_size = point.size;

            for (int4 i = 0; i < data.numCalls(); ++i)
            {
                fc = data.getCallSpecs(i);
                if (fc->getExtraPop() == 0) continue; // Stack pointer is undisturbed
                op = data.newOp(2, fc->getOp()->getAddr());
                data.newVarnodeOut(sb_size, sb_addr, op);
                data.opSetInput(op, data.newVarnode(sb_size, sb_addr), 0);
                if (fc->getExtraPop() != ProtoModel::extrapop_unknown)
                { // We know exactly how stack pointer is changed
                    fc->setEffectiveExtraPop(fc->getExtraPop());
                    data.opSetOpcode(op, CPUI_INT_ADD);
                    data.opSetInput(op, data.newConstant(sb_size, fc->getExtraPop()), 1);
                    data.opInsertAfter(op, fc->getOp());
                }
                else
                {           // We don't know exactly, so we create INDIRECT
                    data.opSetOpcode(op, CPUI_INDIRECT);
                    data.opSetInput(op, data.newVarnodeIop(fc->getOp()), 1);
                    data.opInsertBefore(op, fc->getOp());
                }
            }
            return 0;
        }
    }
}
