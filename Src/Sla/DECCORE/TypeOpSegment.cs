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
            : base(t, CPUI_SEGMENTOP,"segmentop")

        {
            opflags = PcodeOp::special | PcodeOp::nocollapse;
            behave = new OpBehavior(CPUI_SEGMENTOP, false, true); // Dummy behavior
        }

        //  virtual Datatype *getOutputLocal(const PcodeOp *op) const;
        //  virtual Datatype *getInputLocal(const PcodeOp *op,int4 slot) const;

        public override Datatype getInputCast(PcodeOp op, int4 slot, CastStrategy castStrategy)
        {
            return (Datatype*)0;        // Never need a cast for inputs
        }

        public override Datatype getOutputToken(PcodeOp op, CastStrategy castStrategy)
        {
            return op->getIn(2)->getHighTypeReadFacing(op); // Assume type of ptr portion
        }

        public override Datatype propagateType(Datatype alttype, PcodeOp op, Varnode invn, Varnode outvn,
            int4 inslot, int4 outslot)
        {
            // Must propagate  slot2 <-> output
            if ((inslot == 0) || (inslot == 1)) return (Datatype*)0;
            if ((outslot == 0) || (outslot == 1)) return (Datatype*)0;
            if (invn->isSpacebase()) return (Datatype*)0;
            type_metatype metain = alttype->getMetatype();
            if (metain != TYPE_PTR) return (Datatype*)0;
            AddrSpace* spc = tlst->getArch()->getDefaultDataSpace();
            Datatype* btype = ((TypePointer*)alttype)->getPtrTo();
            return tlst->getTypePointer(outvn->getSize(), btype, spc->getWordSize());
        }

        public override void push(PrintLanguage lng, PcodeOp op, PcodeOp readOp)
        {
            lng->opSegmentOp(op);
        }

        public override void printRaw(TextWriter s, PcodeOp op)
        {
            if (op->getOut() != (Varnode*)0)
            {
                Varnode::printRaw(s, op->getOut());
                s << " = ";
            }
            s << getOperatorName(op);
            s << '(';
            AddrSpace* spc = op->getIn(0)->getSpaceFromConst();
            s << spc->getName() << ',';
            Varnode::printRaw(s, op->getIn(1));
            s << ',';
            Varnode::printRaw(s, op->getIn(2));
            s << ')';
        }
    }
}
