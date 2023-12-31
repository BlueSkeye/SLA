﻿
namespace Sla.DECCORE
{
    /// \brief An operation that writes to volatile memory
    ///
    /// This CALLOTHER p-code operation takes as its input parameters:
    ///   - Constant id
    ///   - Reference Varnode to the memory being written
    ///   - The Varnode value being written to the memory
    internal class VolatileWriteOp : VolatileOp
    {
        public VolatileWriteOp(Architecture g, string nm,int ind,bool functional)
            : base(g, nm, ind)
        {
            flags = functional ? 0 : userop_flags.annotation_assignment;
        }

        public override string getOperatorName(PcodeOp op)
        {
            if (op.numInput() < 3) return name;
            return appendSize(name, op.getIn(2).getSize());
        }

        public override int extractAnnotationSize(Varnode vn, PcodeOp op)
        {
            // Get size from the 3rd parameter of write function
            return op.getIn(2).getSize();
        }
    }
}
