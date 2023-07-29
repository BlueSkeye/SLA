using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    internal abstract class RHSConstant
    {
        // A construction that results in a constant on the right-hand side of an expression
        ~RHSConstant()
        {
        }
        public abstract RHSConstant clone();

        public abstract uintb getConstant(UnifyState state);

        public abstract void writeExpression(TextWriter s, UnifyCPrinter printstate);
    }
}
