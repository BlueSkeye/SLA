using Sla.DECCORE;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief Information about the SEGMENTOP op-code
    ///
    /// The segment operator is a placeholder for address mappings
    /// (i.e. from virtual to physical) that a compiler (or processor)
    /// may generate as part of its memory model. Typically this is
    /// of little concern to the high-level code, so this scheme allows
    /// the decompiler to track it but ignore it where appropriate,
    /// such as in type propagation and printing high-level expressions
    internal class TypeOpSegment : TypeOp
    {
        public TypeOpSegment(TypeFactory t)
            : base(t, OpCode.CPUI_SEGMENTOP,"segmentop")

        {
            opflags = PcodeOp.Flags.special | PcodeOp.Flags.nocollapse;
            behave = new OpBehavior(CPUI_SEGMENTOP, false, true); // Dummy behavior
        }

        //  virtual Datatype *getOutputLocal(PcodeOp *op) const;
        //  virtual Datatype *getInputLocal(PcodeOp *op,int slot) const;

        public override Datatype getInputCast(PcodeOp op, int slot, CastStrategy castStrategy)
        {
            return (Datatype)null;        // Never need a cast for inputs
        }

        public override Datatype getOutputToken(PcodeOp op, CastStrategy castStrategy)
        {
            return op.getIn(2).getHighTypeReadFacing(op); // Assume type of ptr portion
        }

        public override Datatype propagateType(Datatype alttype, PcodeOp op, Varnode invn, Varnode outvn,
            int inslot, int outslot)
        {
            // Must propagate  slot2 <. output
            if ((inslot == 0) || (inslot == 1)) return (Datatype)null;
            if ((outslot == 0) || (outslot == 1)) return (Datatype)null;
            if (invn.isSpacebase()) return (Datatype)null;
            type_metatype metain = alttype.getMetatype();
            if (metain != type_metatype.TYPE_PTR) return (Datatype)null;
            AddrSpace* spc = tlst.getArch().getDefaultDataSpace();
            Datatype* btype = ((TypePointer*)alttype).getPtrTo();
            return tlst.getTypePointer(outvn.getSize(), btype, spc.getWordSize());
        }

        public override void push(PrintLanguage lng, PcodeOp op, PcodeOp readOp)
        {
            lng.opSegmentOp(op);
        }

        public override void printRaw(TextWriter s, PcodeOp op)
        {
            if (op.getOut() != (Varnode)null)
            {
                Varnode::printRaw(s, op.getOut());
                s << " = ";
            }
            s << getOperatorName(op);
            s << '(';
            AddrSpace* spc = op.getIn(0).getSpaceFromConst();
            s << spc.getName() << ',';
            Varnode::printRaw(s, op.getIn(1));
            s << ',';
            Varnode::printRaw(s, op.getIn(2));
            s << ')';
        }
    }
}
