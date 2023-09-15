using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief An operation that reads from volatile memory
    ///
    /// This CALLOTHER p-code operation takes as its input parameter, after the constant id,
    /// a reference Varnode to the memory being read. The output returned by this operation
    /// is the actual value read from memory.
    internal class VolatileReadOp : VolatileOp
    {
        public VolatileReadOp(Architecture g, string nm,int ind,bool functional)
            : base(g, nm, ind)
        {
            flags = functional ? 0 : userop_flags.no_operator;
        }

        public override string getOperatorName(PcodeOp op)
        {
            if (op.getOut() == (Varnode)null) return name;
            return appendSize(name, op.getOut().getSize());
        }

        public override int extractAnnotationSize(Varnode vn, PcodeOp op)
        {
            Varnode outvn = op.getOut();
            if (outvn != (Varnode)null)
                return op.getOut().getSize(); // Get size from output of read function
            return 1;
        }
    }
}
