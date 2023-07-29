using Sla.DECCORE;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    internal class ConstantConsumed : RHSConstant
    {
        // A varnode's consume mask
        private int4 varindex;
        
        public ConstantConsumed(int4 ind)
        {
            varindex = ind;
        }
        
        public override RHSConstant clone() => new ConstantConsumed(varindex);

        public override uintb getConstant(UnifyState state)
        {
            Varnode* vn = state.data(varindex).getVarnode();
            return vn.getConsume();
        }

        public override void writeExpression(TextWriter s, UnifyCPrinter printstate)
        {
            s << printstate.getName(varindex) << ".getConsume()";
        }
    }
}
