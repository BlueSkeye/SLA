using Sla.DECCORE;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    internal class ConstantOffset : RHSConstant
    {
        // A varnode's offset
        private int4 varindex;
        
        public ConstantOffset(int4 ind)
        {
            varindex = ind;
        }
        
        public override RHSConstant clone() => new ConstantOffset(varindex);

        public override uintb getConstant(UnifyState state)
        {
            Varnode* vn = state.data(varindex).getVarnode();
            return vn.getOffset();
        }

        public override void writeExpression(TextWriter s, UnifyCPrinter printstate)
        {
            s << printstate.getName(varindex) << ".getOffset()";
        }
    }
}
