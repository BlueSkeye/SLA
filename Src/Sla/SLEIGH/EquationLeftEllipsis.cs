
namespace Sla.SLEIGH
{
    internal abstract class EquationLeftEllipsis : PatternEquation
    {
        // Equation preceded by ellipses
        private PatternEquation eq;

        ~EquationLeftEllipsis()
        {
            PatternEquation.release(eq);
        }

        public EquationLeftEllipsis(PatternEquation e)
        {
            (eq = e).layClaim();
        }
    
        public override void genPattern(List<TokenPattern> ops)
        {
            eq.genPattern(ops);
            resultpattern = eq.getTokenPattern();
            resultpattern.setLeftEllipsis(true);
        }

        public override bool resolveOperandLeft(OperandResolve state)
        {
            int cur_base = state.@base;
            state.@base = -2;
            bool res = eq.resolveOperandLeft(state);
            if (!res) return false;
            state.@base = cur_base;
            return true;
        }

        public override void operandOrder(Constructor ct, List<OperandSymbol> order)
        {
            // List operands
            eq.operandOrder(ct, order);
        }
    }
}
