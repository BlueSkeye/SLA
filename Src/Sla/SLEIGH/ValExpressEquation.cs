
namespace Sla.SLEIGH
{
    internal abstract class ValExpressEquation : PatternEquation
    {
        protected PatternValue lhs;
        protected PatternExpression rhs;
        
        ~ValExpressEquation()
        {
            PatternExpression.release(lhs);
            PatternExpression.release(rhs);
        }

        public ValExpressEquation(PatternValue l, PatternExpression r)
        {
            (lhs = l).layClaim();
            (rhs = r).layClaim();
        }

        public override bool resolveOperandLeft(OperandResolve state)
        {
            state.cur_rightmost = -1;
            if (resultpattern.getLeftEllipsis() || resultpattern.getRightEllipsis()) // don't know length
                state.size = -1;
            else
                state.size = resultpattern.getMinimumLength();
            return true;
        }
    }
}
