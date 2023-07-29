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
        private int constindex;
        
        public ConstantNamed(int id)
        {
            constindex = id;
        }
        
        public int getId() => constindex;

        public override RHSConstant clone() => new ConstantNamed(constindex);

        public override ulong getConstant(UnifyState state) => state.data(constindex).getConstant();

        public override void writeExpression(TextWriter s, UnifyCPrinter printstate)
        {
            s << printstate.getName(constindex);
        }
    }
}
