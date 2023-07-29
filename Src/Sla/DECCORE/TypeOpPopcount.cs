using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    internal class TypeOpPopcount : TypeOpFunc
    {
        public TypeOpPopcount(TypeFactory t)
            : base(t, CPUI_POPCOUNT,"POPCOUNT", TYPE_INT, TYPE_UNKNOWN)
        {
            opflags = PcodeOp::unary;
            behave = new OpBehaviorPopcount();
        }

        public override void push(PrintLanguage lng, PcodeOp op, PcodeOp readOp)
        {
            lng.opPopcountOp(op);
        }
    }
}
