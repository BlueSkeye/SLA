using Sla.DECCORE;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    internal class TypeOpIntAdd : TypeOpBinary
    {
        public TypeOpIntAdd(TypeFactory t)
            : base(t, CPUI_INT_ADD,"+", TYPE_INT, TYPE_INT)
        {
            opflags = PcodeOp::binary | PcodeOp::commutative;
            addlflags = arithmetic_op | inherits_sign;
            behave = new OpBehaviorIntAdd();
        }

        public override void push(PrintLanguage lng, PcodeOp op, PcodeOp readOp)
        {
            lng.opIntAdd(op);
        }

        public override Datatype getOutputToken(PcodeOp op, CastStrategy castStrategy)
        {
            return castStrategy.arithmeticOutputStandard(op);  // Use arithmetic typing rules
        }

        public override Datatype propagateType(Datatype alttype, PcodeOp op, Varnode invn, Varnode outvn,
            int4 inslot, int4 outslot)
        {
            type_metatype invnMeta = alttype.getMetatype();
            if (invnMeta != TYPE_PTR)
            {
                if (invnMeta != TYPE_INT && invnMeta != TYPE_UINT)
                    return (Datatype*)0;
                if (outslot != 1 || !op.getIn(1).isConstant())
                    return (Datatype*)0;
            }
            else if ((inslot != -1) && (outslot != -1))
                return (Datatype*)0;    // Must propagate input <. output for pointers
            Datatype* newtype;
            if (outvn.isConstant() && (alttype.getMetatype() != TYPE_PTR))
                newtype = alttype;
            else if (inslot == -1)      // Propagating output to input
                newtype = op.getIn(outslot).getTempType();    // Don't propagate pointer types this direction
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
        public static Datatype propagateAddIn2Out(Datatype alttype, TypeFactory typegrp, PcodeOp op,
            int4 inslot)
        {
            TypePointer* pointer = (TypePointer*)alttype;
            uintb uoffset;
            int4 command = propagateAddPointer(uoffset, op, inslot, pointer.getPtrTo().getSize());
            if (command == 2) return op.getOut().getTempType(); // Doesn't look like a good pointer add
            TypePointer* parent = (TypePointer*)0;
            uintb parentOff;
            if (command != 3)
            {
                uoffset = AddrSpace::addressToByte(uoffset, pointer.getWordSize());
                bool allowWrap = (op.code() != CPUI_PTRSUB);
                do
                {
                    pointer = pointer.downChain(uoffset, parent, parentOff, allowWrap, *typegrp);
                    if (pointer == (TypePointer*)0)
                        break;
                } while (uoffset != 0);
            }
            if (parent != (TypePointer*)0)
            {
                // If the innermost containing object is a TYPE_STRUCT or TYPE_ARRAY
                // preserve info about this container
                Datatype* pt;
                if (pointer == (TypePointer*)0)
                    pt = typegrp.getBase(1, TYPE_UNKNOWN); // Offset does not point at a proper sub-type
                else
                    pt = pointer.getPtrTo();   // The sub-type being directly pointed at
                pointer = typegrp.getTypePointerRel(parent, pt, parentOff);
            }
            if (pointer == (TypePointer*)0)
            {
                if (command == 0)
                    return alttype;
                return op.getOut().getTempType();
            }
            if (op.getIn(inslot).isSpacebase())
            {
                if (pointer.getPtrTo().getMetatype() == TYPE_SPACEBASE)
                    pointer = typegrp.getTypePointer(pointer.getSize(), typegrp.getBase(1, TYPE_UNKNOWN), pointer.getWordSize());
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
        public static int4 propagateAddPointer(uintb off, PcodeOp op, int4 slot, int4 sz)
        {
            if (op.code() == CPUI_PTRADD)
            {
                if (slot != 0) return 2;
                Varnode* constvn = op.getIn(1);
                uintb mult = op.getIn(2).getOffset();
                if (constvn.isConstant())
                {
                    off = (constvn.getOffset() * mult) & calc_mask(constvn.getSize());
                    return (off == 0) ? 0 : 1;
                }
                if (sz != 0 && (mult % sz) != 0)
                    return 2;
                return 3;
            }
            if (op.code() == CPUI_PTRSUB)
            {
                if (slot != 0) return 2;
                off = op.getIn(1).getOffset();
                return (off == 0) ? 0 : 1;
            }
            if (op.code() == CPUI_INT_ADD)
            {
                Varnode* othervn = op.getIn(1 - slot);
                // Check if othervn is an offset
                if (!othervn.isConstant())
                {
                    if (othervn.isWritten())
                    {
                        PcodeOp* multop = othervn.getDef();
                        if (multop.code() == CPUI_INT_MULT)
                        {
                            Varnode* constvn = multop.getIn(1);
                            if (constvn.isConstant())
                            {
                                uintb mult = constvn.getOffset();
                                if (mult == calc_mask(constvn.getSize()))  // If multiplying by -1
                                    return 2;       // Assume this is a pointer difference and don't propagate
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
                if (othervn.getTempType().getMetatype() == TYPE_PTR) // Check if othervn marked as ptr
                    return 2;
                off = othervn.getOffset();
                return (off == 0) ? 0 : 1;
            }
            return 2;
        }
    }
}
