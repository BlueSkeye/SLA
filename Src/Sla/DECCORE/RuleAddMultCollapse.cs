using Sla.CORE;

namespace Sla.DECCORE
{
    internal class RuleAddMultCollapse : Rule
    {
        public RuleAddMultCollapse(string g)
            : base(g, 0, "addmultcollapse")
        {
        }

        public override Rule? clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule)null;
            return new RuleAddMultCollapse(getGroup());
        }

        /// \class RuleAddMultCollapse
        /// \brief Collapse constants in an additive or multiplicative expression
        ///
        /// Forms include:
        ///  - `((V + c) + d)  =>  V + (c+d)`
        ///  - `((V * c) * d)  =>  V * (c*d)`
        ///  - `((V + (W + c)) + d)  =>  (W + (c+d)) + V`
        public override void getOpList(List<OpCode> oplist)
        {
            OpCode[] list = { OpCode.CPUI_INT_ADD, OpCode.CPUI_INT_MULT };
            oplist.AddRange(list);
        }

        public override int applyOp(PcodeOp op, Funcdata data)
        {
            Varnode[] c = new Varnode[2];           // Constant varnodes
            Varnode sub;
            Varnode sub2;
            Varnode newvn;
            PcodeOp subop;
            OpCode opc;
            ulong val;

            opc = op.code();
            // Constant is in c[0], other is in sub
            c[0] = op.getIn(1) ?? throw new ApplicationException();
            if (!c[0].isConstant()) return 0; // Neither input is a constant
            sub = op.getIn(0) ?? throw new ApplicationException();
            // Find other constant one level down
            if (!sub.isWritten()) return 0;
            subop = sub.getDef() ?? throw new ApplicationException();
            // Must be same exact operation
            if (subop.code() != opc) return 0;
            c[1] = subop.getIn(1) ?? throw new ApplicationException();
            if (!c[1].isConstant()) {
                // a = ((stackbase + c[1]) + othervn) + c[0]  =>       (stackbase + c[0] + c[1]) + othervn
                // This lets two constant offsets get added together even in the case where there is:
                //    another term getting added in AND
                //    the result of the intermediate sum is used more than once  (otherwise collectterms should pick it up)
                if (opc != OpCode.CPUI_INT_ADD) return 0;
                Varnode othervn;
                Varnode basevn;
                PcodeOp baseop;
                for (int i = 0; i < 2; ++i) {
                    othervn = subop.getIn(i) ?? throw new ApplicationException();
                    if (othervn.isConstant()) continue;
                    if (othervn.isFree()) continue;
                    sub2 = subop.getIn(1 - i) ?? throw new ApplicationException();
                    if (!sub2.isWritten()) continue;
                    baseop = sub2.getDef() ?? throw new ApplicationException();
                    if (baseop.code() != OpCode.CPUI_INT_ADD) continue;
                    c[1] = baseop.getIn(1) ?? throw new ApplicationException();
                    if (!c[1].isConstant()) continue;
                    basevn = baseop.getIn(0) ?? throw new ApplicationException();
                    // Only apply this particular case if we are adding to a base pointer
                    if (!basevn.isSpacebase()) continue;
                    // because this adds a new add operation
                    if (!basevn.isInput()) continue;

                    val = op.getOpcode().evaluateBinary(c[0].getSize(), c[0].getSize(), c[0].getOffset(),
                        c[1].getOffset());
                    newvn = data.newConstant(c[0].getSize(), val);
                    if (c[0].getSymbolEntry() != (SymbolEntry)null)
                        newvn.copySymbolIfValid(c[0]);
                    else if (c[1].getSymbolEntry() != (SymbolEntry)null)
                        newvn.copySymbolIfValid(c[1]);
                    PcodeOp newop = data.newOp(2, op.getAddr());
                    data.opSetOpcode(newop, OpCode.CPUI_INT_ADD);
                    Varnode newout = data.newUniqueOut(c[0].getSize(), newop);
                    data.opSetInput(newop, basevn, 0);
                    data.opSetInput(newop, newvn, 1);
                    data.opInsertBefore(newop, op);
                    data.opSetInput(op, newout, 0);
                    data.opSetInput(op, othervn, 1);
                    return 1;
                }
                return 0;
            }
            sub2 = subop.getIn(0) ?? throw new ApplicationException();
            if (sub2.isFree()) return 0;

            val = op.getOpcode().evaluateBinary(c[0].getSize(), c[0].getSize(), c[0].getOffset(), c[1].getOffset());
            newvn = data.newConstant(c[0].getSize(), val);
            if (c[0].getSymbolEntry() != (SymbolEntry)null)
                newvn.copySymbolIfValid(c[0]);
            else if (c[1].getSymbolEntry() != (SymbolEntry)null)
                newvn.copySymbolIfValid(c[1]);
            data.opSetInput(op, newvn, 1); // Replace c[0] with c[0]+c[1] or c[0]*c[1]
            data.opSetInput(op, sub2, 0); // Replace sub with sub2
            return 1;
        }
    }
}
