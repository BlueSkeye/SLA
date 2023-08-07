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
            : base(t, OpCode.CPUI_PIECE,"CONCAT", type_metatype.TYPE_UNKNOWN, type_metatype.TYPE_UNKNOWN)
        {
            opflags = PcodeOp.Flags.binary;
            behave = new OpBehaviorPiece();
        }

        public override Datatype getInputCast(PcodeOp op, int slot, CastStrategy castStrategy)
        {
            return (Datatype)null;        // Never need a cast into a PIECE
        }

        public override Datatype getOutputToken(PcodeOp op, CastStrategy castStrategy)
        {
            Varnode vn = op.getOut();
            Datatype* dt = vn.getHighTypeDefFacing();
            type_metatype meta = dt.getMetatype();
            if ((meta == type_metatype.TYPE_INT) || (meta == type_metatype.TYPE_UINT))      // PIECE casts to uint or int, based on output
                return dt;
            return tlst.getBase(vn.getSize(), type_metatype.TYPE_UINT); // If output is unknown or pointer, treat as cast to uint
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
