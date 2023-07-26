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
        public abstract TokenPattern genPattern(intb val);

        public override void listValues(List<PatternValue> list)
        {
            list.push_back(this);
        }

        public override void getMinMax(List<intb> minlist, List<intb> maxlist)
        {
            minlist.push_back(minValue()); maxlist.push_back(maxValue());
        }

        public override intb getSubValue(List<intb> replace, int4 listpos) => replace[listpos++];

        public abstract intb minValue();

        public abstract intb maxValue();
    }
}
