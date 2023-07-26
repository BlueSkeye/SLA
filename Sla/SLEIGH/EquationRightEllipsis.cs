using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Sla.SLEIGH
{
    internal class EquationRightEllipsis : PatternEquation
    {
        // Equation preceded by ellipses
        private PatternEquation eq;

        ~EquationRightEllipsis()
        {
            PatternEquation::release(eq);
        }

        public EquationRightEllipsis(PatternEquation e)
        {
            (eq = e)->layClaim();
        }

        public override void genPattern(List<TokenPattern> ops)
        {
            eq->genPattern(ops);
            resultpattern = eq->getTokenPattern();
            resultpattern.setRightEllipsis(true);
        }

        public override bool resolveOperandLeft(OperandResolve state)
        {
            bool res = eq->resolveOperandLeft(state);
            if (!res) return false;
            state.size = -1;        // Cannot predict size
            return true;
        }

        public override void operandOrder(Constructor ct, List<OperandSymbol> order)
        {
            eq->operandOrder(ct, order);    // List operands
        }
    }
}
