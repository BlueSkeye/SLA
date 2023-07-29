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
            intb lhsmin = lhs->minValue();
            intb lhsmax = lhs->maxValue();
            List<PatternValue> semval = new List<PatternValue>();
            vector<intb> min;
            vector<intb> max;
            vector<intb> cur;
            int4 count = 0;

            rhs->listValues(semval);
            rhs->getMinMax(min, max);
            cur = min;

            do
            {
                intb val = rhs->getSubValue(cur);
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
