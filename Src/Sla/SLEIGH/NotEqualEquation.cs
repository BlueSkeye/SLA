using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Text;
using System.Threading.Tasks;

namespace Sla.SLEIGH
{
    internal class NotEqualEquation : ValExpressEquation
    {
        public NotEqualEquation(PatternValue l, PatternExpression r)
            : base(l, r)
        {
        }
        
        public override void genPattern(List<TokenPattern> ops)
        {
            intb lhsmin = lhs.minValue();
            intb lhsmax = lhs.maxValue();
            List<PatternValue> semval = new List<PatternValue>();
            vector<intb> min;
            vector<intb> max;
            vector<intb> cur;
            int4 count = 0;

            rhs.listValues(semval);
            rhs.getMinMax(min, max);
            cur = min;

            do
            {
                intb lhsval;
                intb val = rhs.getSubValue(cur);
                for (lhsval = lhsmin; lhsval <= lhsmax; ++lhsval)
                {
                    if (lhsval == val) continue;
                    if (count == 0)
                        resultpattern = buildPattern(lhs, lhsval, semval, cur);
                    else
                        resultpattern = resultpattern.doOr(buildPattern(lhs, lhsval, semval, cur));
                    count += 1;
                }
            } while (advance_combo(cur, min, max));
            if (count == 0)
                throw SleighError("Notequal constraint is impossible to match");
        }
    }
}
