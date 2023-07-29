using System;
using System.Buffers.Text;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Sla.DECCORE
{
    internal class RuleAddMultCollapse : Rule
    {
        public RuleAddMultCollapse(string g)
            : base(g, 0, "addmultcollapse")
        {
        }

        public override Rule clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule*)0;
            return new RuleAddMultCollapse(getGroup());
        }

        /// \class RuleAddMultCollapse
        /// \brief Collapse constants in an additive or multiplicative expression
        ///
        /// Forms include:
        ///  - `((V + c) + d)  =>  V + (c+d)`
        ///  - `((V * c) * d)  =>  V * (c*d)`
        ///  - `((V + (W + c)) + d)  =>  (W + (c+d)) + V`
        public override void getOpList(List<uint> oplist)
        {
            uint list[] = { CPUI_INT_ADD, CPUI_INT_MULT };
            oplist.insert(oplist.end(), list, list + 2);
        }

        public override int applyOp(PcodeOp op, Funcdata data)
        {
            Varnode* c[2];           // Constant varnodes
            Varnode* sub,*sub2,*newvn;
            PcodeOp* subop;
            OpCode opc;

            opc = op.code();
            // Constant is in c[0], other is in sub
            c[0] = op.getIn(1);
            if (!c[0].isConstant()) return 0; // Neither input is a constant
            sub = op.getIn(0);
            // Find other constant one level down
            if (!sub.isWritten()) return 0;
            subop = sub.getDef();
            if (subop.code() != opc) return 0; // Must be same exact operation
            c[1] = subop.getIn(1);
            if (!c[1].isConstant())
            {
                // a = ((stackbase + c[1]) + othervn) + c[0]  =>       (stackbase + c[0] + c[1]) + othervn
                // This lets two constant offsets get added together even in the case where there is:
                //    another term getting added in AND
                //    the result of the intermediate sum is used more than once  (otherwise collectterms should pick it up)
                if (opc != CPUI_INT_ADD) return 0;
                Varnode* othervn,*basevn;
                PcodeOp* baseop;
                for (int i = 0; i < 2; ++i)
                {
                    othervn = subop.getIn(i);
                    if (othervn.isConstant()) continue;
                    if (othervn.isFree()) continue;
                    sub2 = subop.getIn(1 - i);
                    if (!sub2.isWritten()) continue;
                    baseop = sub2.getDef();
                    if (baseop.code() != CPUI_INT_ADD) continue;
                    c[1] = baseop.getIn(1);
                    if (!c[1].isConstant()) continue;
                    basevn = baseop.getIn(0);
                    if (!basevn.isSpacebase()) continue; // Only apply this particular case if we are adding to a base pointer
                    if (!basevn.isInput()) continue;   // because this adds a new add operation

                    ulong val = op.getOpcode().evaluateBinary(c[0].getSize(), c[0].getSize(), c[0].getOffset(), c[1].getOffset());
                    newvn = data.newConstant(c[0].getSize(), val);
                    if (c[0].getSymbolEntry() != (SymbolEntry*)0)
                        newvn.copySymbolIfValid(c[0]);
                    else if (c[1].getSymbolEntry() != (SymbolEntry*)0)
                        newvn.copySymbolIfValid(c[1]);
                    PcodeOp* newop = data.newOp(2, op.getAddr());
                    data.opSetOpcode(newop, CPUI_INT_ADD);
                    Varnode* newout = data.newUniqueOut(c[0].getSize(), newop);
                    data.opSetInput(newop, basevn, 0);
                    data.opSetInput(newop, newvn, 1);
                    data.opInsertBefore(newop, op);
                    data.opSetInput(op, newout, 0);
                    data.opSetInput(op, othervn, 1);
                    return 1;
                }
                return 0;
            }
            sub2 = subop.getIn(0);
            if (sub2.isFree()) return 0;

            ulong val = op.getOpcode().evaluateBinary(c[0].getSize(), c[0].getSize(), c[0].getOffset(), c[1].getOffset());
            newvn = data.newConstant(c[0].getSize(), val);
            if (c[0].getSymbolEntry() != (SymbolEntry*)0)
                newvn.copySymbolIfValid(c[0]);
            else if (c[1].getSymbolEntry() != (SymbolEntry*)0)
                newvn.copySymbolIfValid(c[1]);
            data.opSetInput(op, newvn, 1); // Replace c[0] with c[0]+c[1] or c[0]*c[1]
            data.opSetInput(op, sub2, 0); // Replace sub with sub2
            return 1;
        }
    }
}
