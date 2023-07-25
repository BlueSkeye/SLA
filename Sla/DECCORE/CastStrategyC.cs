using ghidra;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief Casting strategies that are specific to the C language
    internal class CastStrategyC : CastStrategy
    {
        public virtual int localExtensionType(Varnode vn, PcodeOp op)
        {
            type_metatype meta = vn.getHighTypeReadFacing(op).getMetatype();
            // 1= natural zero extension, 2= natural sign extension
            int natural;
            if ((meta == TYPE_UINT) || (meta == TYPE_BOOL) || (meta == TYPE_UNKNOWN)) {
                natural = UNSIGNED_EXTENSION;
            }
            else if (meta == TYPE_INT) {
                natural = SIGNED_EXTENSION;
            }
            else {
                return UNKNOWN_PROMOTION;
            }
            if (vn->isConstant()) {
                if (!signbit_negative(vn.getOffset(), vn.getSize())) {
                    // If the high-bit is zero
                    // Can be viewed as either extension
                    return EITHER_EXTENSION;
                }
                return natural;
            }
            if (vn.isExplicit()) {
                return natural;
            }
            if (!vn.isWritten()) {
                return UNKNOWN_PROMOTION;
            }
            PcodeOp defOp = vn.getDef();
            if (defOp.isBoolOutput()) {
                return EITHER_EXTENSION;
            }
            OpCode opc = defOp.code();
            if ((opc == CPUI_CAST) || (opc == CPUI_LOAD) || defOp->isCall()) {
                return natural;
            }
            if (opc == CPUI_INT_AND) {
                // This is kind of recursing
                Varnode tmpvn = defOp.getIn(1);
                if (tmpvn.isConstant()) {
                    return (!signbit_negative(tmpvn.getOffset(), tmpvn.getSize()))
                        ? EITHER_EXTENSION
                        : natural;
                }
            }
            return UNKNOWN_PROMOTION;
        }

        public virtual int intPromotionType(Varnode vn)
        {
            int val;
            if (vn.getSize() >= promoteSize) {
                return NO_PROMOTION;
            }
            if (vn.isConstant()) {
                return localExtensionType(vn, vn->loneDescend());
            }
            if (vn.isExplicit()) {
                return NO_PROMOTION;
            }
            if (!vn.isWritten()) {
                return UNKNOWN_PROMOTION;
            }
            PcodeOp op = vn.getDef();
            Varnode othervn;
            switch (op.code()) {
                case CPUI_INT_AND:
                    othervn = op.getIn(1);
                    if ((localExtensionType(othervn, op) & UNSIGNED_EXTENSION) != 0) {
                        return UNSIGNED_EXTENSION;
                    }
                    othervn = op.getIn(0);
                    if ((localExtensionType(othervn, op) & UNSIGNED_EXTENSION) != 0) {
                        // If either side has zero extension, result has zero extension
                        return UNSIGNED_EXTENSION;
                    }
                    break;
                case CPUI_INT_RIGHT:
                    othervn = op.getIn(0);
                    val = localExtensionType(othervn, op);
                    if ((val & UNSIGNED_EXTENSION) != 0) {
                        // If the input provably zero extends
                        // then the result is a zero extension (plus possibly a sign extension)
                        return val;
                    }
                    break;
                case CPUI_INT_SRIGHT:
                    othervn = op.getIn(0);
                    val = localExtensionType(othervn, op);
                    if ((val & SIGNED_EXTENSION) != 0) {
                        // If input can be construed as a sign-extension
                        // then the result is a sign extension (plus possibly a zero extension)
                        return val;
                    }
                    break;
                case CPUI_INT_XOR:
                case CPUI_INT_OR:
                case CPUI_INT_DIV:
                case CPUI_INT_REM:
                    othervn = op.getIn(0);
                    if ((localExtensionType(othervn, op) & UNSIGNED_EXTENSION) == 0) {
                        return UNKNOWN_PROMOTION;
                    }
                    othervn = op.getIn(1);
                    if ((localExtensionType(othervn, op) & UNSIGNED_EXTENSION) == 0) {
                        return UNKNOWN_PROMOTION;
                    }
                    // If both sides have zero extension, result has zero extension
                    return UNSIGNED_EXTENSION;
                case CPUI_INT_SDIV:
                case CPUI_INT_SREM:
                    othervn = op.getIn(0);
                    if ((localExtensionType(othervn, op) & SIGNED_EXTENSION) == 0) {
                        return UNKNOWN_PROMOTION;
                    }
                    othervn = op.getIn(1);
                    if ((localExtensionType(othervn, op) & SIGNED_EXTENSION) == 0) {
                        return UNKNOWN_PROMOTION;
                    }
                    // If both sides have sign extension, result has sign extension
                    return SIGNED_EXTENSION;
                case CPUI_INT_NEGATE:
                case CPUI_INT_2COMP:
                    othervn = op.getIn(0);
                    if ((localExtensionType(othervn, op) & SIGNED_EXTENSION) != 0) {
                        return SIGNED_EXTENSION;
                    }
                    break;
                case CPUI_INT_ADD:
                case CPUI_INT_SUB:
                case CPUI_INT_LEFT:
                case CPUI_INT_MULT:
                    break;
                default:
                    // No integer promotion at all
                    return NO_PROMOTION;
            }
            return UNKNOWN_PROMOTION;
        }

        public bool checkIntPromotionForCompare(PcodeOp op, int slot)
        {
            Varnode vn = op.getIn(slot);
            int exttype1 = intPromotionType(vn);
            if (exttype1 == NO_PROMOTION) {
                return false;
            }
            if (exttype1 == UNKNOWN_PROMOTION) {
                // If there is promotion and we don't know type, we need a cast
                return true;
            }
            int exttype2 = intPromotionType(op.getIn(1 - slot));
            if ((exttype1 & exttype2) != 0) {
                // If both sides share a common extension, then these bits aren't determining factor
                return false;
            }
            if (exttype2 == NO_PROMOTION) {
                // other side would not have integer promotion, but our side is forcing it
                // but both sides get extended in the same way
                return false;
            }
            return true;
        }

        public bool checkIntPromotionForExtension(PcodeOp op)
        {
            Varnode vn = op.getIn(0);
            int exttype = intPromotionType(vn);
            if (exttype == NO_PROMOTION) {
                return false;
            }
            if (exttype == UNKNOWN_PROMOTION) {
                // If there is an extension and we don't know type, we need a cast
                return true;
            }

            // Test if the promotion extension matches the explicit extension
            if (((exttype & UNSIGNED_EXTENSION) != 0) && (op.code() == CPUI_INT_ZEXT)) {
                return false;
            }
            if (((exttype & SIGNED_EXTENSION) != 0) && (op.code() == CPUI_INT_SEXT)) {
                return false;
            }
            // Otherwise we need a cast before we extend
            return true;
        }

        public bool isExtensionCastImplied(PcodeOp op, PcodeOp readOp)
        {
            Varnode outVn = op.getOut();
            if (outVn.isExplicit()) {
            }
            else {
                if (readOp == null) {
                    return false;
                }
                type_metatype metatype = outVn.getHighTypeReadFacing(readOp).getMetatype();
                Varnode otherVn;
                int slot;
                switch (readOp.code()) {
                    case CPUI_PTRADD:
                        break;
                    case CPUI_INT_ADD:
                    case CPUI_INT_SUB:
                    case CPUI_INT_MULT:
                    case CPUI_INT_DIV:
                    case CPUI_INT_AND:
                    case CPUI_INT_OR:
                    case CPUI_INT_XOR:
                    case CPUI_INT_EQUAL:
                    case CPUI_INT_NOTEQUAL:
                    case CPUI_INT_LESS:
                    case CPUI_INT_LESSEQUAL:
                    case CPUI_INT_SLESS:
                    case CPUI_INT_SLESSEQUAL:
                        slot = readOp.getSlot(outVn);
                        otherVn = readOp.getIn(1 - slot);
                        // Check if the expression involves an explicit variable of the right integer type
                        if (otherVn.isConstant()) {
                            // Integer tokens do not naturally indicate their size, and
                            // integers that are bigger than the promotion size are NOT naturally extended.
                            if (otherVn.getSize() > promoteSize) {
                                // So if the integer is bigger than the promotion size
                                // The extension cast on the other side must be explicit
                                return false;
                            }
                        }
                        else if (!otherVn.isExplicit()) {
                            return false;
                        }
                        if (otherVn.getHighTypeReadFacing(readOp).getMetatype() != metatype) {
                            return false;
                        }
                        break;
                    default:
                        return false;
                }
                // Everything is integer promotion
                return true;
            }
            return false;
        }

        public Datatype? castStandard(Datatype reqtype, Datatype curtype, bool care_uint_int,
            bool care_ptr_uint)
        {
            // Generic casting rules that apply for most ops
            if (curtype == reqtype) {
                // Types are equal, no cast required
                return null;
            }
            Datatype reqbase = reqtype;
            Datatype curbase = curtype;
            bool isptr = false;
            while ((reqbase.getMetatype() == TYPE_PTR) && (curbase.getMetatype() == TYPE_PTR)) {
                TypePointer reqptr = (TypePointer)reqbase;
                TypePointer curptr = (TypePointer)curbase;
                if (reqptr.getWordSize() != curptr.getWordSize()) {
                    return reqtype;
                }
                if (reqptr.getSpace() != curptr.getSpace()) {
                    if (reqptr.getSpace() != null && curptr.getSpace() != null) {
                        // Pointers to different address spaces.  We must cast
                        // If one pointer doesn't have an address, assume a conversion to/from sub-type and don't need a cast
                        return reqtype;
                    }
                }
                reqbase = reqptr.getPtrTo();
                curbase = curptr.getPtrTo();
                care_uint_int = true;
                isptr = true;
            }
            while (reqbase->getTypedef() != null) {
                reqbase = reqbase.getTypedef();
            }
            while (curbase.getTypedef() != null) {
                curbase = curbase.getTypedef();
            }
            // Different typedefs could point to the same type
            if (curbase == reqbase) {
                return null;
            }
            if ((reqbase.getMetatype() == TYPE_VOID) || (curtype.getMetatype() == TYPE_VOID)) {
                // Don't cast from or to VOID
                return null;
            }
            if (reqbase.getSize() != curbase.getSize()) {
                if (reqbase.isVariableLength() && isptr && reqbase.hasSameVariableBase(curbase)) {
                    // Don't need a cast
                    return null;
                }
                // Otherwise, always cast change in size
                return reqtype;
            }
            switch (reqbase.getMetatype()) {
                case TYPE_UNKNOWN:
                    return null;
                case TYPE_UINT:
                    if (!care_uint_int) {
                        type_metatype meta = curbase.getMetatype();
                        // Note: meta can be TYPE_UINT if curbase is typedef/enumerated
                        if ((meta == TYPE_UNKNOWN) || (meta == TYPE_INT) || (meta == TYPE_UINT) || (meta == TYPE_BOOL)) {
                            return null;
                        }
                    }
                    else {
                        type_metatype meta = curbase.getMetatype();
                        if ((meta == TYPE_UINT) || (meta == TYPE_BOOL)) {
                            // Can be TYPE_UINT for typedef/enumerated
                            return null;
                        }
                        if (isptr && (meta == TYPE_UNKNOWN)) {
                            // Don't cast pointers to unknown
                            return null;
                        }
                    }
                    if ((!care_ptr_uint) && (curbase->getMetatype() == TYPE_PTR)) {
                        return null;
                    }
                    break;
                case TYPE_INT:
                    if (!care_uint_int) {
                        type_metatype meta = curbase.getMetatype();
                        // Note: meta can be TYPE_INT if curbase is an enumerated type
                        if ((meta == TYPE_UNKNOWN) || (meta == TYPE_INT) || (meta == TYPE_UINT) || (meta == TYPE_BOOL)) {
                            return null;
                        }
                    }
                    else {
                        type_metatype meta = curbase.getMetatype();
                        if ((meta == TYPE_INT) || (meta == TYPE_BOOL)) {
                            // Can be TYPE_INT for typedef/enumerated/char
                            return null;
                        }
                        if (isptr && (meta == TYPE_UNKNOWN)) {
                            // Don't cast pointers to unknown
                            return null;
                        }
                    }
                    break;
                case TYPE_CODE:
                    if (curbase.getMetatype() == TYPE_CODE) {
                        // Don't cast between function pointer and generic code pointer
                        if (((TypeCode)reqbase).getPrototype() == null) {
                            return null;
                        }
                        if (((TypeCode)curbase).getPrototype() == null) {
                            return null;
                        }
                    }
                    break;
                default:
                    break;
            }
            return reqtype;
        }

        public Datatype arithmeticOutputStandard(PcodeOp op)
        {
            Datatype res1 = op.getIn(0).getHighTypeReadFacing(op);
            if (res1.getMetatype() == TYPE_BOOL) {
                // Treat boolean as if it is cast to an integer
                res1 = tlst.getBase(res1.getSize(), TYPE_INT);
            }
            Datatype res2;

            for (int i = 1; i < op.numInput(); ++i) {
                res2 = op.getIn(i).getHighTypeReadFacing(op);
                if (res2.getMetatype() == TYPE_BOOL) {
                    continue;
                }
                if (0 > res2.typeOrder(*res1)) {
                    res1 = res2;
                }
            }
            return res1;
        }

        public bool isSubpieceCast(Datatype outtype, Datatype intype, uint offset)
        {
            if (offset != 0) {
                return false;
            }
            type_metatype inmeta = intype.getMetatype();
            if ((inmeta != TYPE_INT) &&
                (inmeta != TYPE_UINT) &&
                (inmeta != TYPE_UNKNOWN) &&
                (inmeta != TYPE_PTR))
            {
                return false;
            }
            type_metatype outmeta = outtype.getMetatype();
            if ((outmeta != TYPE_INT) &&
                (outmeta != TYPE_UINT) &&
                (outmeta != TYPE_UNKNOWN) &&
                (outmeta != TYPE_PTR) &&
                (outmeta != TYPE_FLOAT))
            {
                return false;
            }
            if (inmeta == TYPE_PTR) {
                if (outmeta == TYPE_PTR) {
                    if (outtype.getSize() < intype.getSize()) {
                        // Cast from far pointer to near pointer
                        return true;
                    }
                }
                if ((outmeta != TYPE_INT) && (outmeta != TYPE_UINT)) {
                    //other casts don't make sense for pointers
                    return false;
                }
            }
            return true;
        }

        public bool isSubpieceCastEndian(Datatype outtype, Datatype intype, uint offset,
            bool isbigend)
        {
            uint tmpoff = offset;
            if (isbigend) {
                tmpoff = intype->getSize() - 1 - offset;
            }
            return isSubpieceCast(outtype, intype, tmpoff);
        }

        public bool isSextCast(Datatype outtype, Datatype intype)
        {
            type_metatype metaout = outtype.getMetatype();
            if (metaout != TYPE_UINT && metaout != TYPE_INT) {
                return false;
            }
            type_metatype metain = intype.getMetatype();
            // Casting to larger storage always extends based on signedness of the input data-type
            // So the input must be SIGNED in order to treat SEXT as a cast
            return ((metain == TYPE_INT) || (metain = TYPE_BOOL));
        }

        public bool isZextCast(Datatype outtype, Datatype intype)
        {
            type_metatype metaout = outtype.getMetatype();
            if (metaout != TYPE_UINT && metaout != TYPE_INT) {
                return false;
            }
            type_metatype metain = intype.getMetatype();
            // Casting to larger storage always extends based on signedness of the input data-type
            // So the input must be UNSIGNED in order to treat ZEXT as a cast
            return ((metain == TYPE_UINT) || (metain == TYPE_BOOL));
        }
    }
}
