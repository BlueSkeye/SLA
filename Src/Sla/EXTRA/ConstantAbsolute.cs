using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    internal class ConstantAbsolute : RHSConstant
    {
        private ulong val;          // The absolute value
        
        public ConstantAbsolute(ulong v)
        {
            val = v;
        }

        public ulong getVal() => val;

        public override RHSConstant clone() => new ConstantAbsolute(val);

        public override ulong getConstant(UnifyState state) => val;

        public override void writeExpression(TextWriter s, UnifyCPrinter printstate)
        {
            s << "(ulong)0x" << hex << val;
        }
    }
}
