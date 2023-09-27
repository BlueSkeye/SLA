
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
            List<long> min = new List<long>();
            List<long> max =  new List<long>();
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
                        resultpattern = Globals.buildPattern(lhs, val, semval, cur);
                    else
                        resultpattern = resultpattern.doOr(Globals.buildPattern(lhs, val, semval, cur));
                    count += 1;
                }
            } while (advance_combo(cur, min, max));
            if (count == 0)
                throw new SleighError("Equal constraint is impossible to match");
        }
    }
}
