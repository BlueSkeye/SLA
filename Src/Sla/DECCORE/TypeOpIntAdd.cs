using Sla.CORE;

namespace Sla.DECCORE
{
    internal class TypeOpIntAdd : TypeOpBinary
    {
        public TypeOpIntAdd(TypeFactory t)
            : base(t, OpCode.CPUI_INT_ADD, "+", type_metatype.TYPE_INT,
                  type_metatype.TYPE_INT)
        {
            opflags = PcodeOp.Flags.binary | PcodeOp.Flags.commutative;
            addlflags = OperationType.arithmetic_op | OperationType.inherits_sign;
            behave = new OpBehaviorIntAdd();
        }

        public override void push(PrintLanguage lng, PcodeOp op, PcodeOp readOp)
        {
            lng.opIntAdd(op);
        }

        public override Datatype getOutputToken(PcodeOp op, CastStrategy castStrategy)
        {
            // Use arithmetic typing rules
            return castStrategy.arithmeticOutputStandard(op);
        }

        public override Datatype? propagateType(Datatype alttype, PcodeOp op, Varnode invn,
            Varnode outvn, int inslot, int outslot)
        {
            type_metatype invnMeta = alttype.getMetatype();
            if (invnMeta != type_metatype.TYPE_PTR) {
                if (invnMeta != type_metatype.TYPE_INT && invnMeta != type_metatype.TYPE_UINT)
                    return (Datatype)null;
                if (outslot != 1 || !op.getIn(1).isConstant())
                    return (Datatype)null;
            }
            else if ((inslot != -1) && (outslot != -1))
                // Must propagate input <. output for pointers
                return (Datatype)null;
            Datatype newtype;
            if (outvn.isConstant() && (alttype.getMetatype() != type_metatype.TYPE_PTR))
                newtype = alttype;
            else if (inslot == -1)
                // Propagating output to input
                // Don't propagate pointer types this direction
                newtype = op.getIn(outslot).getTempType();
            else
                newtype = propagateAddIn2Out(alttype, tlst, op, inslot);
            return newtype;
        }

        /// \brief Propagate a pointer data-type through an ADD operation.
        ///
        /// Assuming a pointer data-type from an ADD PcodeOp propagates from an input to
        /// its output, calculate the transformed data-type of the output Varnode, which
        /// will depend on details of the operation. If the edge doesn't make sense as
        /// "an ADD to a pointer", prevent the propagation by returning the output Varnode's
        /// current data-type.
        /// \param alttype is the resolved input pointer data-type
        /// \param typegrp is the TypeFactory for constructing the transformed Datatype
        /// \param op is the ADD operation
        /// \param inslot is the edge to propagate along
        /// \return the transformed Datatype or the original output Datatype
        public static Datatype propagateAddIn2Out(Datatype alttype, TypeFactory typegrp,
            PcodeOp op, int inslot)
        {
            TypePointer pointer = (TypePointer)alttype;
            ulong uoffset;
            int command = propagateAddPointer(uoffset, op, inslot, pointer.getPtrTo().getSize());
            // Doesn't look like a good pointer add
            if (command == 2)
                return op.getOut().getTempType();
            TypePointer parent = (TypePointer)null;
            ulong parentOff;
            if (command != 3) {
                uoffset = AddrSpace.addressToByte(uoffset, pointer.getWordSize());
                bool allowWrap = (op.code() != OpCode.CPUI_PTRSUB);
                do {
                    pointer = pointer.downChain(uoffset, parent, parentOff, allowWrap, typegrp);
                    if (pointer == (TypePointer)null)
                        break;
                } while (uoffset != 0);
            }
            if (parent != (TypePointer)null) {
                // If the innermost containing object is a type_metatype.TYPE_STRUCT or type_metatype.TYPE_ARRAY
                // preserve info about this container
                Datatype pt = (pointer == (TypePointer)null)
                    // Offset does not point at a proper sub-type
                    ? typegrp.getBase(1, type_metatype.TYPE_UNKNOWN)
                    // The sub-type being directly pointed at
                    : pointer.getPtrTo();
                pointer = typegrp.getTypePointerRel(parent, pt, parentOff);
            }
            if (pointer == (TypePointer)null) {
                if (command == 0)
                    return alttype;
                return op.getOut().getTempType();
            }
            if (op.getIn(inslot).isSpacebase()) {
                if (pointer.getPtrTo().getMetatype() == type_metatype.TYPE_SPACEBASE)
                    pointer = typegrp.getTypePointer(pointer.getSize(),
                        typegrp.getBase(1, type_metatype.TYPE_UNKNOWN), pointer.getWordSize());
            }
            return pointer;
        }

        /// Determine if the given data-type edge looks like a pointer
        /// propagating through an "add a constant" operation. We assume the input
        /// to the edge has a pointer data-type.  This routine returns one the commands:
        ///   - 0  indicates this is "add a constant" adding a zero  (PTRSUB or PTRADD)
        ///   - 1  indicates this is "add a constant" and the constant is passed back
        ///   - 2  indicating the pointer does not propagate through
        ///   - 3  the input data-type propagates through untransformed
        ///
        /// \param off passes back the constant offset if the command is '0' or '1'
        /// \param op is the PcodeOp propagating the data-type
        /// \param slot is the input edge being propagated
        /// \param sz is the size of the data-type being pointed to
        /// \return a command indicating how the op should be treated
        public static int propagateAddPointer(ulong off, PcodeOp op, int slot, int sz)
        {
            if (op.code() == OpCode.CPUI_PTRADD) {
                if (slot != 0) return 2;
                Varnode constvn = op.getIn(1) ?? throw new ApplicationException();
                ulong mult = op.getIn(2).getOffset();
                if (constvn.isConstant()) {
                    off = (constvn.getOffset() * mult)
                        & Globals.calc_mask((uint)constvn.getSize());
                    return (off == 0) ? 0 : 1;
                }
                if (sz != 0 && (mult % sz) != 0)
                    return 2;
                return 3;
            }
            if (op.code() == OpCode.CPUI_PTRSUB) {
                if (slot != 0) return 2;
                off = op.getIn(1).getOffset();
                return (off == 0) ? 0 : 1;
            }
            if (op.code() == OpCode.CPUI_INT_ADD) {
                Varnode othervn = op.getIn(1 - slot) ?? throw new ApplicationException();
                // Check if othervn is an offset
                if (!othervn.isConstant()) {
                    if (othervn.isWritten()) {
                        PcodeOp multop = othervn.getDef() ?? throw new ApplicationException();
                        if (multop.code() == OpCode.CPUI_INT_MULT) {
                            Varnode constvn = multop.getIn(1) ?? throw new ApplicationException();
                            if (constvn.isConstant()) {
                                ulong mult = constvn.getOffset();
                                // If multiplying by -1
                                if (mult == Globals.calc_mask((uint)constvn.getSize()))
                                    // Assume this is a pointer difference and don't propagate
                                    return 2;
                                if (sz != 0 && (mult % sz) != 0)
                                    return 2;
                            }
                            return 3;
                        }
                    }
                    if (sz == 1)
                        return 3;
                    return 2;
                }
                // Check if othervn marked as ptr
                if (othervn.getTempType().getMetatype() == type_metatype.TYPE_PTR)
                    return 2;
                off = othervn.getOffset();
                return (off == 0) ? 0 : 1;
            }
            return 2;
        }
    }
}
