using Sla.CORE;

namespace Sla.DECCORE
{
    /// \brief Information about the LOAD op-code
    internal class TypeOpLoad : TypeOp
    {
        public TypeOpLoad(TypeFactory t)
            : base(t, OpCode.CPUI_LOAD,"load")
        {
            opflags = PcodeOp.Flags.special | PcodeOp.Flags.nocollapse;
            behave = new OpBehavior(OpCode.CPUI_LOAD, false, true); // Dummy behavior
        }

        //  virtual Datatype *getInputLocal(const PcodeOp *op,int slot);
        public override Datatype getInputCast(PcodeOp op, int slot, CastStrategy castStrategy)
        {
            if (slot != 1) return (Datatype)null;
            Datatype reqtype = op.getOut().getHighTypeDefFacing();   // Cast load pointer to match output
            Varnode invn = op.getIn(1);
            Datatype curtype = invn.getHighTypeReadFacing(op);
            AddrSpace spc = op.getIn(0).getSpaceFromConst();
            // Its possible that the input type is not a pointer to the output type
            // (or even a pointer) due to cycle trimming in the type propagation algorithms
            if (curtype.getMetatype() == type_metatype.TYPE_PTR)
                curtype = ((TypePointer)curtype).getPtrTo();
            else
                return tlst.getTypePointer(invn.getSize(), reqtype, spc.getWordSize());
            if ((curtype != reqtype) && (curtype.getSize() == reqtype.getSize())) {
                // If we have a non-standard  in = ptr a  out = b  (a!=b)
                // We may want to postpone casting BEFORE the load in favor of casting AFTER the load
                type_metatype curmeta = curtype.getMetatype();
                if ((curmeta != type_metatype.TYPE_STRUCT) && (curmeta != type_metatype.TYPE_ARRAY) && (curmeta != type_metatype.TYPE_SPACEBASE) && (curmeta != type_metatype.TYPE_UNION)) {
                    // if the input is a pointer to a primitive type
                    if ((!invn.isImplied()) || (!invn.isWritten()) || (invn.getDef().code() != OpCode.CPUI_CAST))
                        return (Datatype)null;    // Postpone cast to output
                                                // If we reach here, the input is a CAST to the wrong type
                                                // We fallthru (returning the proper input case) so that the bad cast can either be
                                                // adjusted or we recast
                }
            }
            reqtype = castStrategy.castStandard(reqtype, curtype, false, true);
            if (reqtype == (Datatype)null) return reqtype;
            return tlst.getTypePointer(invn.getSize(), reqtype, spc.getWordSize());
        }

        public override Datatype getOutputToken(PcodeOp op, CastStrategy castStrategy)
        {
            Datatype ct = op.getIn(1).getHighTypeReadFacing(op);
            if ((ct.getMetatype() == type_metatype.TYPE_PTR)
                && (((TypePointer)ct).getPtrTo().getSize() == op.getOut().getSize()))
                return ((TypePointer)ct).getPtrTo();
            //  return TypeOp::getOutputToken(op);
            // The input to the load is not a pointer or (more likely)
            // points to something of a different size than the output
            // In this case, there will have to be a cast, so we assume
            // the cast will cause the load to produce the type matching
            // its output
            return op.getOut().getHighTypeDefFacing();
        }

        public override Datatype propagateType(Datatype alttype, PcodeOp op, Varnode invn, Varnode outvn,
            int inslot, int outslot)
        {
            if ((inslot == 0) || (outslot == 0)) return (Datatype)null; // Don't propagate along this edge
            if (invn.isSpacebase()) return (Datatype)null;
            Datatype newtype;
            if (inslot == -1) {
                // Propagating output to input (value to ptr)
                AddrSpace spc = op.getIn(0).getSpaceFromConst();
                newtype = tlst.getTypePointerNoDepth(outvn.getTempType().getSize(), alttype, spc.getWordSize());
            }
            else if (alttype.getMetatype() == type_metatype.TYPE_PTR) {
                newtype = ((TypePointer)alttype).getPtrTo();
                if (newtype.getSize() != outvn.getTempType().getSize() || newtype.isVariableLength()) // Size must be appropriate
                    newtype = outvn.getTempType();
            }
            else
                newtype = outvn.getTempType(); // Don't propagate anything
            return newtype;
        }

        public override void push(PrintLanguage lng, PcodeOp op, PcodeOp readOp)
        {
            lng.opLoad(op);
        }

        public override void printRaw(TextWriter s, PcodeOp op)
        {
            Varnode.printRaw(s, op.getOut());
            s.Write(" = *(");
            AddrSpace spc = op.getIn(0).getSpaceFromConst();
            s.Write($"{spc.getName()},");
            Varnode.printRaw(s, op.getIn(1));
            s.Write(')');
        }
    }
}
