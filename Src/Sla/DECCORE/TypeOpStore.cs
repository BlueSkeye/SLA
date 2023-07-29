using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief Information about the STORE op-code
    internal class TypeOpStore : TypeOp
    {
        public TypeOpStore(TypeFactory t)
        {
            opflags = PcodeOp::special | PcodeOp::nocollapse;
            behave = new OpBehavior(CPUI_STORE, false, true); // Dummy behavior
        }

        //  virtual Datatype *getInputLocal(PcodeOp *op,int slot) const;

        public override Datatype getInputCast(PcodeOp op, int slot, CastStrategy castStrategy)
        {
            if (slot == 0) return (Datatype*)0;
            Varnode pointerVn = op.getIn(1);
            Datatype* pointerType = pointerVn.getHighTypeReadFacing(op);
            Datatype* pointedToType = pointerType;
            Datatype* valueType = op.getIn(2).getHighTypeReadFacing(op);
            AddrSpace* spc = op.getIn(0).getSpaceFromConst();
            int destSize;
            if (pointerType.getMetatype() == TYPE_PTR)
            {
                pointedToType = ((TypePointer*)pointerType).getPtrTo();
                destSize = pointedToType.getSize();
            }
            else
                destSize = -1;
            if (destSize != valueType.getSize())
            {
                if (slot == 1)
                    return tlst.getTypePointer(pointerVn.getSize(), valueType, spc.getWordSize());
                else
                    return (Datatype*)0;
            }
            if (slot == 1)
            {
                if (pointerVn.isWritten() && pointerVn.getDef().code() == CPUI_CAST)
                {
                    if (pointerVn.isImplied() && pointerVn.loneDescend() == op)
                    {
                        // CAST is already in place, test if it is casting to the right type
                        Datatype* newType = tlst.getTypePointer(pointerVn.getSize(), valueType, spc.getWordSize());
                        if (pointerType != newType)
                            return newType;
                    }
                }
                return (Datatype*)0;
            }
            // If we reach here, cast the value, not the pointer
            return castStrategy.castStandard(pointedToType, valueType, false, true);
        }

        public override Datatype propagateType(Datatype alttype, PcodeOp op, Varnode invn, Varnode outvn,
            int inslot, int outslot)
        {
            if ((inslot == 0) || (outslot == 0)) return (Datatype*)0; // Don't propagate along this edge
            if (invn.isSpacebase()) return (Datatype*)0;
            Datatype* newtype;
            if (inslot == 2)
            {       // Propagating value to ptr
                AddrSpace* spc = op.getIn(0).getSpaceFromConst();
                newtype = tlst.getTypePointerNoDepth(outvn.getTempType().getSize(), alttype, spc.getWordSize());
            }
            else if (alttype.getMetatype() == TYPE_PTR)
            {
                newtype = ((TypePointer*)alttype).getPtrTo();
                if (newtype.getSize() != outvn.getTempType().getSize() || newtype.isVariableLength())
                    newtype = outvn.getTempType();
            }
            else
                newtype = outvn.getTempType(); // Don't propagate anything
            return newtype;
        }

        public override void push(PrintLanguage lng, PcodeOp op, PcodeOp readOp)
        {
            lng.opStore(op);
        }

        public override void printRaw(TextWriter s, PcodeOp op)
        {
            s << "*(";
            AddrSpace* spc = op.getIn(0).getSpaceFromConst();
            s << spc.getName() << ',';
            Varnode::printRaw(s, op.getIn(1));
            s << ") = ";
            Varnode::printRaw(s, op.getIn(2));
        }
    }
}
