using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    internal class ConstantNamed : RHSConstant
    {
        private int4 constindex;
        
        public ConstantNamed(int4 id)
        {
            constindex = id;
        }
        
        public int4 getId() => constindex;

        public override RHSConstant clone() => new ConstantNamed(constindex);

        public override uintb getConstant(UnifyState state) => state.data(constindex).getConstant();

        public override void writeExpression(TextWriter s, UnifyCPrinter printstate)
        {
            s << printstate.getName(constindex);
        }
    }
}
