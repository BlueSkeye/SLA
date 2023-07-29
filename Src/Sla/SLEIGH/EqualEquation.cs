using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Text;
using System.Threading.Tasks;

namespace Sla.SLEIGH
{
    internal class EqualEquation : ValExpressEquation
    {
        public EqualEquation(PatternValue l, PatternExpression r)
            : base(l, r)
        {
        }
        
        public override void genPattern(List<TokenPattern> ops)
        {
            long lhsmin = lhs.minValue();
            long lhsmax = lhs.maxValue();
            List<PatternValue> semval = new List<PatternValue>();
            List<long> min;
            List<long> max;
            List<long> cur;
            int count = 0;

            rhs.listValues(semval);
            rhs.getMinMax(min, max);
            cur = min;

            do
            {
                long val = rhs.getSubValue(cur);
                if ((val >= lhsmin) && (val <= lhsmax))
                {
                    if (count == 0)
                        resultpattern = buildPattern(lhs, val, semval, cur);
                    else
                        resultpattern = resultpattern.doOr(buildPattern(lhs, val, semval, cur));
                    count += 1;
                }
            } while (advance_combo(cur, min, max));
            if (count == 0)
                throw SleighError("Equal constraint is impossible to match");
        }
    }
}
