using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Sla.SLEIGH
{
    internal class OperandEquation : PatternEquation
    {
        // Equation that defines operand
        private int4 index;
        
        public OperandEquation(int4 ind)
        {
            index = ind;
        }
        
        public override void genPattern(List<TokenPattern> ops)
        {
            resultpattern = ops[index];
        }

        public override bool resolveOperandLeft(OperandResolve state)
        {
            OperandSymbol* sym = state.operands[index];
            if (sym.isOffsetIrrelevant())
            {
                sym.offsetbase = -1;
                sym.reloffset = 0;
                return true;
            }
            if (state.@base == -2)		// We have no base
                return false;
            sym.offsetbase = state.@base;
            sym.reloffset = state.offset;
            state.cur_rightmost = index;
            state.size = 0;     // Distance from right edge
            return true;
        }

        public override void operandOrder(Constructor ct, List<OperandSymbol> order)
        {
            OperandSymbol* sym = ct.getOperand(index);
            if (!sym.isMarked())
            {
                order.push_back(sym);
                sym.setMark();
            }
        }
    }
}
