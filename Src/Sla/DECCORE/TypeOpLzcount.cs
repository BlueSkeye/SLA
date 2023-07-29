using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief Information about the LZCOUNT op-code
    internal class TypeOpLzcount : TypeOpFunc
    {
        public TypeOpLzcount(TypeFactory t)
            : base(t, CPUI_LZCOUNT,"LZCOUNT", TYPE_INT, TYPE_UNKNOWN)
        {
            opflags = PcodeOp::unary;
            behave = new OpBehaviorLzcount();
        }

        public override void push(PrintLanguage lng, PcodeOp op, PcodeOp readOp)
        {
            lng.opLzcountOp(op);
        }
    }
}
