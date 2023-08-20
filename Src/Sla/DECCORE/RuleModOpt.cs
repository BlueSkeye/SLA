using Sla.CORE;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using static ghidra.TraceDAG.BlockTrace;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Sla.DECCORE
{
    internal class RuleModOpt : Rule
    {
        public RuleModOpt(string g)
            : base(g, 0, "modopt")
        {
        }

        public override Rule? clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule)null;
            return new RuleModOpt(getGroup());
        }

        /// \class RuleModOpt
        /// \brief Simplify expressions that optimize INT_REM and INT_SREM
        public override void getOpList(List<OpCode> oplist)
        {
            oplist.Add(OpCode.CPUI_INT_DIV);
            oplist.Add(OpCode.CPUI_INT_SDIV);
        }

        public override int applyOp(PcodeOp op, Funcdata data)
        {
            PcodeOp multop;
            PcodeOp addop;
            Varnode div;
            Varnode x;
            Varnode outvn;
            Varnode outvn2;
            Varnode div2;

            x = op.getIn(0);
            div = op.getIn(1);
            outvn = op.getOut();
            IEnumerator<PcodeOp> iter1 = outvn.beginDescend();
            while (iter1.MoveNext()) {
                multop = iter1.Current;
                if (multop.code() != OpCode.CPUI_INT_MULT) continue;
                div2 = multop.getIn(1);
                if (div2 == outvn)
                    div2 = multop.getIn(0);
                // Check that div is 2's complement of div2
                if (div2.isConstant())
                {
                    if (!div.isConstant()) continue;
                    ulong mask = Globals.calc_mask(div2.getSize());
                    if ((((div2.getOffset() ^ mask) + 1) & mask) != div.getOffset())
                        continue;
                }
                else
                {
                    if (!div2.isWritten()) continue;
                    if (div2.getDef().code() != OpCode.CPUI_INT_2COMP) continue;
                    if (div2.getDef().getIn(0) != div) continue;
                }
                outvn2 = multop.getOut();
                IEnumerator<PcodeOp> iter2 = outvn2.beginDescend();
                while (iter2.MoveNext()) {
                    addop = iter2.Current;
                    if (addop.code() != OpCode.CPUI_INT_ADD) continue;
                    Varnode lvn = addop.getIn(0);
                    if (lvn == outvn2)
                        lvn = addop.getIn(1);
                    if (lvn != x) continue;
                    data.opSetInput(addop, x, 0);
                    if (div.isConstant())
                        data.opSetInput(addop, data.newConstant(div.getSize(), div.getOffset()), 1);
                    else
                        data.opSetInput(addop, div, 1);
                    if (op.code() == OpCode.CPUI_INT_DIV) // Remainder of proper signedness
                        data.opSetOpcode(addop, OpCode.CPUI_INT_REM);
                    else
                        data.opSetOpcode(addop, OpCode.CPUI_INT_SREM);
                    return 1;
                }
            }
            return 0;
        }
    }
}
