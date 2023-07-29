using Sla.DECCORE;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    internal class ConstantVarnodeSize : RHSConstant
    {
        // A varnode's size as an actual constant
        private int varindex;
        
        public ConstantVarnodeSize(int ind)
        {
            varindex = ind;
        }
        
        public override RHSConstant clone() => new ConstantVarnodeSize(varindex);

        public override ulong getConstant(UnifyState state)
        {
            Varnode* vn = state.data(varindex).getVarnode();
            return (ulong)vn.getSize();    // The size is the actual value
        }

        public override void writeExpression(TextWriter s, UnifyCPrinter printstate)
        {
            s << "(ulong)" << printstate.getName(varindex) << ".getSize()";
        }
    }
}
