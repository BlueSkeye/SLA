using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Text;
using System.Threading.Tasks;

namespace Sla.SLEIGH
{
    internal class LessEquation: ValExpressEquation
    {
        public LessEquation(PatternValue l, PatternExpression r)
            : base(l, r)
        {
        }
        
        public override void genPattern(List<TokenPattern> ops)
        {
            long lhsmin = lhs.minValue();
            long lhsmax = lhs.maxValue();
            List<PatternValue> semval = new List<PatternValue>();
            List<long> min = new List<long>();
            List<long> max = new List<long>();
            int count = 0;

            rhs.listValues(semval);
            rhs.getMinMax(min, max);
            List<long> cur = min;

            do {
                long lhsval;
                long val = rhs.getSubValue(cur);
                for (lhsval = lhsmin; lhsval <= lhsmax; ++lhsval) {
                    if (lhsval >= val)
                        continue;
                    if (count == 0)
                        resultpattern = Globals.buildPattern(lhs, lhsval, semval, cur);
                    else
                        resultpattern = resultpattern.doOr(
                            Globals.buildPattern(lhs, lhsval, semval, cur));
                    count += 1;
                }
            } while (advance_combo(cur, min, max));
            if (count == 0)
                throw new SleighError("Less than constraint is impossible to match");
        }
    }
}
