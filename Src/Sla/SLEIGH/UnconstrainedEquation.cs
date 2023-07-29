using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Sla.SLEIGH
{
    internal class UnconstrainedEquation : PatternEquation
    {
        // Unconstrained equation, just get tokens
        private PatternExpression patex;
        
        ~UnconstrainedEquation()
        {
            PatternExpression::release(patex);
        }

        public UnconstrainedEquation(PatternExpression p)
        {
            (patex = p).layClaim();
        }

        public override void genPattern(List<TokenPattern> ops)
        {
            resultpattern = patex.genMinPattern(ops);
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
