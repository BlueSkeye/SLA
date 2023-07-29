using Sla.DECCORE;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    internal class ConstantNZMask : RHSConstant
    {
        // A varnode's non-zero mask
        private  int4 varindex;
        
        public ConstantNZMask(int4 ind)
        {
            varindex = ind;
        }
        
        public override RHSConstant clone() => new ConstantNZMask(varindex);

        public override uintb getConstant(UnifyState state)
        {
            Varnode* vn = state.data(varindex).getVarnode();
            return vn->getNZMask();
        }

        public override void writeExpression(TextWriter s, UnifyCPrinter printstate)
        {
            s << printstate.getName(varindex) << "->getNZMask()";
        }
    }
}
