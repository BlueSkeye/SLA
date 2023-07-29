using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief Information about the INT_EQUAL op-code
    internal class TypeOpEqual : TypeOpBinary
    {
        public TypeOpEqual(TypeFactory t)
            : base(t, CPUI_INT_EQUAL, "==", TYPE_BOOL, TYPE_INT)
        {
            opflags = PcodeOp::binary | PcodeOp::booloutput | PcodeOp::commutative;
            addlflags = inherits_sign;
            behave = new OpBehaviorEqual();
        }

        public override void push(PrintLanguage lng, PcodeOp op, PcodeOp readOp)
        {
            lng.opIntEqual(op);
        }

        public override Datatype getInputCast(PcodeOp op, int slot, CastStrategy castStrategy)
        {
            Datatype* reqtype = op.getIn(0).getHighTypeReadFacing(op);    // Input arguments should be the same type
            Datatype* othertype = op.getIn(1).getHighTypeReadFacing(op);
            if (0 > othertype.typeOrder(*reqtype))
                reqtype = othertype;
            if (castStrategy.checkIntPromotionForCompare(op, slot))
                return reqtype;
            othertype = op.getIn(slot).getHighTypeReadFacing(op);
            return castStrategy.castStandard(reqtype, othertype, false, false);
        }

        public override Datatype propagateType(Datatype alttype, PcodeOp op, Varnode invn, Varnode outvn,
            int inslot, int outslot)
        {
            return TypeOpEqual::propagateAcrossCompare(alttype, tlst, invn, outvn, inslot, outslot);
        }

        /// \brief Propagate a given data-type across a \e comparison PcodeOp
        ///
        /// This implements the propagateType() method for multiple p-code operators:
        ///   CPUI_INT_EQUAL, CPUI_INT_NOTEQUAL, CPUI_INT_LESS, etc.
        /// The propagation must be across the input Varnodes of the comparison.
        /// \param alttype is the incoming data-type to propagate
        /// \param typegrp is the TypeFactory used for constructing transformed data-types
        /// \param invn is the Varnode holding the incoming data-type
        /// \param outvn is the Varnode that will hold the outgoing data-type
        /// \param inslot indicates how the incoming Varnode is attached to the PcodeOp (-1 indicates output >= indicates input)
        /// \param outslot indicates how the outgoing Varnode is attached to the PcodeOp
        /// \return the outgoing data-type or null (to indicate no propagation)
        public static Datatype propagateAcrossCompare(Datatype alttype, TypeFactory typegrp, Varnode invn,
            Varnode outvn, int inslot, int outslot)
        {
            if (inslot == -1 || outslot == -1) return (Datatype*)0;
            Datatype* newtype;
            if (invn.isSpacebase())
            {
                AddrSpace* spc = typegrp.getArch().getDefaultDataSpace();
                newtype = typegrp.getTypePointer(alttype.getSize(), typegrp.getBase(1, TYPE_UNKNOWN), spc.getWordSize());
            }
            else if (alttype.isPointerRel() && !outvn.isConstant())
            {
                TypePointerRel* relPtr = (TypePointerRel*)alttype;
                if (relPtr.getParent().getMetatype() == TYPE_STRUCT && relPtr.getPointerOffset() >= 0)
                {
                    // If we know the pointer is in the middle of a structure, don't propagate across comparison operators
                    // as the two sides of the operator are likely to be different types , and the other side can also
                    // get data-type information from the structure pointer
                    newtype = typegrp.getTypePointer(relPtr.getSize(), typegrp.getBase(1, TYPE_UNKNOWN), relPtr.getWordSize());
                }
                else
                    newtype = alttype;
            }
            else
                newtype = alttype;
            return newtype;
        }
    }
}
