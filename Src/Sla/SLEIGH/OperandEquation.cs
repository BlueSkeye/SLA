
namespace Sla.SLEIGH
{
    internal class OperandEquation : PatternEquation
    {
        // Equation that defines operand
        private int index;
        
        public OperandEquation(int ind)
        {
            index = ind;
        }
        
        public override void genPattern(List<TokenPattern> ops)
        {
            resultpattern = ops[index];
        }

        public override bool resolveOperandLeft(OperandResolve state)
        {
            OperandSymbol sym = state.operands[index];
            if (sym.isOffsetIrrelevant()) {
                sym.offsetbase = -1;
                sym.reloffset = 0;
                return true;
            }
            if (state.@base == -2)
                // We have no base
                return false;
            sym.offsetbase = state.@base;
            sym.reloffset = state.offset;
            state.cur_rightmost = index;
            // Distance from right edge
            state.size = 0;
            return true;
        }

        public override void operandOrder(Constructor ct, List<OperandSymbol> order)
        {
            OperandSymbol sym = ct.getOperand(index);
            if (!sym.isMarked()) {
                order.Add(sym);
                sym.setMark();
            }
        }
    }
}
