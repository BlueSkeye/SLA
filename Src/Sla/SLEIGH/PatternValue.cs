using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Sla.SLEIGH
{
    internal abstract class PatternValue : PatternExpression
    {
        public abstract TokenPattern genPattern(long val);

        public override void listValues(List<PatternValue> list)
        {
            list.push_back(this);
        }

        public override void getMinMax(List<long> minlist, List<long> maxlist)
        {
            minlist.push_back(minValue()); maxlist.push_back(maxValue());
        }

        public override long getSubValue(List<long> replace, int listpos) => replace[listpos++];

        public abstract long minValue();

        public abstract long maxValue();
    }
}
