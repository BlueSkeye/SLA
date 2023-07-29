using Sla.DECCORE;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    internal class ConstantIsConstant : RHSConstant
    {
        private int4 varindex;
        
        public ConstantIsConstant(int4 ind)
        {
            varindex = ind;
        }
        
        public override RHSConstant clone() => new ConstantIsConstant(varindex);

        public override uintb getConstant(UnifyState state)
        {
            Varnode* vn = state.data(varindex).getVarnode();
            return vn->isConstant() ? (uintb)1 : (uintb)0;
        }

        public override void writeExpression(TextWriter s, UnifyCPrinter printstate)
        {
            s << "(uintb)" << printstate.getName(varindex) << "->isConstant()";
        }
    }
}
