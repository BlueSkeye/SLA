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
        private uintb val;          // The absolute value
        
        public ConstantAbsolute(uintb v)
        {
            val = v;
        }

        public uintb getVal() => val;

        public override RHSConstant clone() => new ConstantAbsolute(val);

        public override uintb getConstant(UnifyState state) => val;

        public override void writeExpression(TextWriter s, UnifyCPrinter printstate)
        {
            s << "(uintb)0x" << hex << val;
        }
    }
}
