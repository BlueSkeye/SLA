using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    internal class DummyConstConstraint : UnifyConstraint
    {
        private int4 constindex;
        
        public DummyConstConstraint(int4 ind)
        {
            maxnum = constindex = ind;
        }
        
        public override UnifyConstraint clone() => (new DummyConstConstraint(constindex)).copyid(this);

        public override bool step(UnifyState state) => true;

        public override void collectTypes(List<UnifyDatatype> typelist)
        {
            typelist[constindex] = UnifyDatatype(UnifyDatatype::const_type);
        }

        public override int4 getBaseIndex() => constindex;

        public override void print(TextWriter s, UnifyCPrinter printstate)
        {
        }

        public override bool isDummy() => true;
    }
}
