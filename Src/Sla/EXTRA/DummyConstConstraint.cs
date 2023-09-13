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
        private int constindex;
        
        public DummyConstConstraint(int ind)
        {
            maxnum = constindex = ind;
        }
        
        public override UnifyConstraint clone() => (new DummyConstConstraint(constindex)).copyid(this);

        public override bool step(UnifyState state) => true;

        public override void collectTypes(List<UnifyDatatype> typelist)
        {
            typelist[constindex] = new UnifyDatatype(UnifyDatatype.TypeKind.const_type);
        }

        public override int getBaseIndex() => constindex;

        public override void print(TextWriter s, UnifyCPrinter printstate)
        {
        }

        public override bool isDummy() => true;
    }
}
