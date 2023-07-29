using ghidra;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    internal class TypeOpPiece : TypeOpFunc
    {
        public TypeOpPiece(TypeFactory t)
            : base(t, CPUI_PIECE,"CONCAT", TYPE_UNKNOWN, TYPE_UNKNOWN)
        {
            opflags = PcodeOp::binary;
            behave = new OpBehaviorPiece();
        }

        public override Datatype getInputCast(PcodeOp op, int slot, CastStrategy castStrategy)
        {
            return (Datatype*)0;        // Never need a cast into a PIECE
        }

        public override Datatype getOutputToken(PcodeOp op, CastStrategy castStrategy)
        {
            Varnode vn = op.getOut();
            Datatype* dt = vn.getHighTypeDefFacing();
            type_metatype meta = dt.getMetatype();
            if ((meta == TYPE_INT) || (meta == TYPE_UINT))      // PIECE casts to uint or int, based on output
                return dt;
            return tlst.getBase(vn.getSize(), TYPE_UINT); // If output is unknown or pointer, treat as cast to uint
        }

        public override string getOperatorName(PcodeOp op)
        {
            ostringstream s;

            s << name << dec << op.getIn(0).getSize() << op.getIn(1).getSize();
            return s.str();
        }

        public override void push(PrintLanguage lng, PcodeOp op, PcodeOp readOp)
        {
            lng.opPiece(op);
        }
    }
}
