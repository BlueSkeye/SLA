using Sla.DECCORE;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    internal class ConstantHeritageKnown : RHSConstant
    {
        // A varnode's consume mask
        private int varindex;
        
        public ConstantHeritageKnown(int ind)
        {
            varindex = ind;
        }
        
        public override RHSConstant clone() => new ConstantHeritageKnown(varindex);

        public override ulong getConstant(UnifyState state)
        {
            Varnode* vn = state.data(varindex).getVarnode();
            return (ulong)(vn.isHeritageKnown() ? 1 : 0);
        }

        public override void writeExpression(TextWriter s, UnifyCPrinter printstate)
        {
            s << "(ulong)" << printstate.getName(varindex) << ".isHeritageKnown()";
        }
    }
}
