using Sla.CORE;
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
        public override IntPromotionCode localExtensionType(Varnode vn, PcodeOp op)
        {
            type_metatype meta = vn.getHighTypeReadFacing(op).getMetatype();
            // 1= natural zero extension, 2= natural sign extension
            IntPromotionCode natural;
            if ((meta == type_metatype.TYPE_UINT) || (meta == type_metatype.TYPE_BOOL) || (meta == type_metatype.TYPE_UNKNOWN)) {
                natural = IntPromotionCode.UNSIGNED_EXTENSION;
            }
            else if (meta == type_metatype.TYPE_INT) {
                natural = IntPromotionCode.SIGNED_EXTENSION;
            }
            else {
                return IntPromotionCode.UNKNOWN_PROMOTION;
            }
            if (vn.isConstant()) {
                if (!signbit_negative(vn.getOffset(), vn.getSize())) {
                    // If the high-bit is zero
                    // Can be viewed as either extension
                    return IntPromotionCode.EITHER_EXTENSION;
                }
                return natural;
            }
            if (vn.isExplicit()) {
                return natural;
            }
            if (!vn.isWritten()) {
                return IntPromotionCode.UNKNOWN_PROMOTION;
            }
            PcodeOp defOp = vn.getDef() ?? throw new BugException();
            if (defOp.isBoolOutput()) {
                return IntPromotionCode.EITHER_EXTENSION;
            }
            OpCode opc = defOp.code();
            if ((opc == OpCode.CPUI_CAST) || (opc == OpCode.CPUI_LOAD) || defOp.isCall()) {
                return natural;
            }
            if (opc == OpCode.CPUI_INT_AND) {
                // This is kind of recursing
                Varnode tmpvn = defOp.getIn(1);
                if (tmpvn.isConstant()) {
                    return (!Globals.signbit_negative(tmpvn.getOffset(), tmpvn.getSize()))
                        ? IntPromotionCode.EITHER_EXTENSION
                        : natural;
                }
            }
            return IntPromotionCode.UNKNOWN_PROMOTION;
        }

        public override IntPromotionCode intPromotionType(Varnode vn)
        {
            IntPromotionCode val;
            if (vn.getSize() >= promoteSize) {
                return IntPromotionCode.NO_PROMOTION;
            }
            if (vn.isConstant()) {
                return localExtensionType(vn, vn.loneDescend());
            }
            if (vn.isExplicit()) {
                return IntPromotionCode.NO_PROMOTION;
            }
            if (!vn.isWritten()) {
                return IntPromotionCode.UNKNOWN_PROMOTION;
            }
            PcodeOp op = vn.getDef() ?? throw new BugException();
            Varnode othervn;
            switch (op.code()) {
                case OpCode.CPUI_INT_AND:
                    othervn = op.getIn(1);
                    if ((localExtensionType(othervn, op) & IntPromotionCode.UNSIGNED_EXTENSION) != 0) {
                        return IntPromotionCode.UNSIGNED_EXTENSION;
                    }
                    othervn = op.getIn(0);
                    if ((localExtensionType(othervn, op) & IntPromotionCode.UNSIGNED_EXTENSION) != 0) {
                        // If either side has zero extension, result has zero extension
                        return IntPromotionCode.UNSIGNED_EXTENSION;
                    }
                    break;
                case OpCode.CPUI_INT_RIGHT:
                    othervn = op.getIn(0);
                    val = localExtensionType(othervn, op);
                    if ((val & IntPromotionCode.UNSIGNED_EXTENSION) != 0) {
                        // If the input provably zero extends
                        // then the result is a zero extension (plus possibly a sign extension)
                        return val;
                    }
                    break;
                case OpCode.CPUI_INT_SRIGHT:
                    othervn = op.getIn(0);
                    val = localExtensionType(othervn, op);
                    if ((val & IntPromotionCode.SIGNED_EXTENSION) != 0) {
                        // If input can be construed as a sign-extension
                        // then the result is a sign extension (plus possibly a zero extension)
                        return val;
                    }
                    break;
                case OpCode.CPUI_INT_XOR:
                case OpCode.CPUI_INT_OR:
                case OpCode.CPUI_INT_DIV:
                case OpCode.CPUI_INT_REM:
                    othervn = op.getIn(0);
                    if ((localExtensionType(othervn, op) & IntPromotionCode.UNSIGNED_EXTENSION) == 0) {
                        return IntPromotionCode.UNKNOWN_PROMOTION;
                    }
                    othervn = op.getIn(1);
                    if ((localExtensionType(othervn, op) & IntPromotionCode.UNSIGNED_EXTENSION) == 0) {
                        return IntPromotionCode.UNKNOWN_PROMOTION;
                    }
                    // If both sides have zero extension, result has zero extension
                    return IntPromotionCode.UNSIGNED_EXTENSION;
                case OpCode.CPUI_INT_SDIV:
                case OpCode.CPUI_INT_SREM:
                    othervn = op.getIn(0);
                    if ((localExtensionType(othervn, op) & IntPromotionCode.SIGNED_EXTENSION) == 0) {
                        return IntPromotionCode.UNKNOWN_PROMOTION;
                    }
                    othervn = op.getIn(1);
                    if ((localExtensionType(othervn, op) & IntPromotionCode.SIGNED_EXTENSION) == 0) {
                        return IntPromotionCode.UNKNOWN_PROMOTION;
                    }
                    // If both sides have sign extension, result has sign extension
                    return IntPromotionCode.SIGNED_EXTENSION;
                case OpCode.CPUI_INT_NEGATE:
                case OpCode.CPUI_INT_2COMP:
                    othervn = op.getIn(0);
                    if ((localExtensionType(othervn, op) & IntPromotionCode.SIGNED_EXTENSION) != 0) {
                        return IntPromotionCode.SIGNED_EXTENSION;
                    }
                    break;
                case OpCode.CPUI_INT_ADD:
                case OpCode.CPUI_INT_SUB:
                case OpCode.CPUI_INT_LEFT:
                case OpCode.CPUI_INT_MULT:
                    break;
                default:
                    // No integer promotion at all
                    return IntPromotionCode.NO_PROMOTION;
            }
            return IntPromotionCode.UNKNOWN_PROMOTION;
        }

        public override bool checkIntPromotionForCompare(PcodeOp op, int slot)
        {
            Varnode vn = op.getIn(slot);
            IntPromotionCode exttype1 = intPromotionType(vn);
            if (exttype1 == IntPromotionCode.NO_PROMOTION) {
                return false;
            }
            if (exttype1 == IntPromotionCode.UNKNOWN_PROMOTION) {
                // If there is promotion and we don't know type, we need a cast
                return true;
            }
            IntPromotionCode exttype2 = intPromotionType(op.getIn(1 - slot));
            if ((exttype1 & exttype2) != 0) {
                // If both sides share a common extension, then these bits aren't determining factor
                return false;
            }
            if (exttype2 == IntPromotionCode.NO_PROMOTION) {
                // other side would not have integer promotion, but our side is forcing it
                // but both sides get extended in the same way
                return false;
            }
            return true;
        }

        public override bool checkIntPromotionForExtension(PcodeOp op)
        {
            Varnode vn = op.getIn(0);
            IntPromotionCode exttype = intPromotionType(vn);
            if (exttype == IntPromotionCode.NO_PROMOTION) {
                return false;
            }
            if (exttype == IntPromotionCode.UNKNOWN_PROMOTION) {
                // If there is an extension and we don't know type, we need a cast
                return true;
            }

            // Test if the promotion extension matches the explicit extension
            if (((exttype & IntPromotionCode.UNSIGNED_EXTENSION) != 0) && (op.code() == OpCode.CPUI_INT_ZEXT)) {
                return false;
            }
            if (((exttype & IntPromotionCode.SIGNED_EXTENSION) != 0) && (op.code() == OpCode.CPUI_INT_SEXT)) {
                return false;
            }
            // Otherwise we need a cast before we extend
            return true;
        }

        public override bool isExtensionCastImplied(PcodeOp op, PcodeOp readOp)
        {
            Varnode outVn = op.getOut();
            if (outVn.isExplicit()) {
                return false;
            }
            if (readOp == null) {
                return false;
            }
            type_metatype metatype = outVn.getHighTypeReadFacing(readOp).getMetatype();
            Varnode otherVn;
            int slot;
            switch (readOp.code()) {
                case OpCode.CPUI_PTRADD:
                    break;
                case OpCode.CPUI_INT_ADD:
                case OpCode.CPUI_INT_SUB:
                case OpCode.CPUI_INT_MULT:
                case OpCode.CPUI_INT_DIV:
                case OpCode.CPUI_INT_AND:
                case OpCode.CPUI_INT_OR:
                case OpCode.CPUI_INT_XOR:
                case OpCode.CPUI_INT_EQUAL:
                case OpCode.CPUI_INT_NOTEQUAL:
                case OpCode.CPUI_INT_LESS:
                case OpCode.CPUI_INT_LESSEQUAL:
                case OpCode.CPUI_INT_SLESS:
                case OpCode.CPUI_INT_SLESSEQUAL:
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

        public override Datatype? castStandard(Datatype reqtype, Datatype curtype, bool care_uint_int,
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
            while ((reqbase.getMetatype() == type_metatype.TYPE_PTR)
                && (curbase.getMetatype() == type_metatype.TYPE_PTR))
            {
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
            while (reqbase.getTypedef() != null) {
                reqbase = reqbase.getTypedef();
            }
            while (curbase.getTypedef() != null) {
                curbase = curbase.getTypedef();
            }
            // Different typedefs could point to the same type
            if (curbase == reqbase) {
                return null;
            }
            if ((reqbase.getMetatype() == type_metatype.TYPE_VOID) || (curtype.getMetatype() == type_metatype.TYPE_VOID)) {
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
                case type_metatype.TYPE_UNKNOWN:
                    return null;
                case type_metatype.TYPE_UINT:
                    if (!care_uint_int) {
                        type_metatype meta = curbase.getMetatype();
                        // Note: meta can be type_metatype.TYPE_UINT if curbase is typedef/enumerated
                        if ((meta == type_metatype.TYPE_UNKNOWN) || (meta == type_metatype.TYPE_INT) || (meta == type_metatype.TYPE_UINT) || (meta == type_metatype.TYPE_BOOL)) {
                            return null;
                        }
                    }
                    else {
                        type_metatype meta = curbase.getMetatype();
                        if ((meta == type_metatype.TYPE_UINT) || (meta == type_metatype.TYPE_BOOL)) {
                            // Can be type_metatype.TYPE_UINT for typedef/enumerated
                            return null;
                        }
                        if (isptr && (meta == type_metatype.TYPE_UNKNOWN)) {
                            // Don't cast pointers to unknown
                            return null;
                        }
                    }
                    if ((!care_ptr_uint) && (curbase.getMetatype() == type_metatype.TYPE_PTR)) {
                        return null;
                    }
                    break;
                case type_metatype.TYPE_INT:
                    if (!care_uint_int) {
                        type_metatype meta = curbase.getMetatype();
                        // Note: meta can be type_metatype.TYPE_INT if curbase is an enumerated type
                        if ((meta == type_metatype.TYPE_UNKNOWN) || (meta == type_metatype.TYPE_INT) || (meta == type_metatype.TYPE_UINT) || (meta == type_metatype.TYPE_BOOL)) {
                            return null;
                        }
                    }
                    else {
                        type_metatype meta = curbase.getMetatype();
                        if ((meta == type_metatype.TYPE_INT) || (meta == type_metatype.TYPE_BOOL)) {
                            // Can be type_metatype.TYPE_INT for typedef/enumerated/char
                            return null;
                        }
                        if (isptr && (meta == type_metatype.TYPE_UNKNOWN)) {
                            // Don't cast pointers to unknown
                            return null;
                        }
                    }
                    break;
                case type_metatype.TYPE_CODE:
                    if (curbase.getMetatype() == type_metatype.TYPE_CODE) {
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

        public override Datatype arithmeticOutputStandard(PcodeOp op)
        {
            Datatype res1 = op.getIn(0).getHighTypeReadFacing(op);
            if (res1.getMetatype() == type_metatype.TYPE_BOOL) {
                // Treat boolean as if it is cast to an integer
                res1 = tlst.getBase(res1.getSize(), type_metatype.TYPE_INT);
            }
            Datatype res2;

            for (int i = 1; i < op.numInput(); ++i) {
                res2 = op.getIn(i).getHighTypeReadFacing(op);
                if (res2.getMetatype() == type_metatype.TYPE_BOOL) {
                    continue;
                }
                if (0 > res2.typeOrder(res1)) {
                    res1 = res2;
                }
            }
            return res1;
        }

        public override bool isSubpieceCast(Datatype outtype, Datatype intype, uint offset)
        {
            if (offset != 0) {
                return false;
            }
            type_metatype inmeta = intype.getMetatype();
            if ((inmeta != type_metatype.TYPE_INT) &&
                (inmeta != type_metatype.TYPE_UINT) &&
                (inmeta != type_metatype.TYPE_UNKNOWN) &&
                (inmeta != type_metatype.TYPE_PTR))
            {
                return false;
            }
            type_metatype outmeta = outtype.getMetatype();
            if ((outmeta != type_metatype.TYPE_INT) &&
                (outmeta != type_metatype.TYPE_UINT) &&
                (outmeta != type_metatype.TYPE_UNKNOWN) &&
                (outmeta != type_metatype.TYPE_PTR) &&
                (outmeta != type_metatype.TYPE_FLOAT))
            {
                return false;
            }
            if (inmeta == type_metatype.TYPE_PTR) {
                if (outmeta == type_metatype.TYPE_PTR) {
                    if (outtype.getSize() < intype.getSize()) {
                        // Cast from far pointer to near pointer
                        return true;
                    }
                }
                if ((outmeta != type_metatype.TYPE_INT) && (outmeta != type_metatype.TYPE_UINT)) {
                    //other casts don't make sense for pointers
                    return false;
                }
            }
            return true;
        }

        public override bool isSubpieceCastEndian(Datatype outtype, Datatype intype, uint offset,
            bool isbigend)
        {
            uint tmpoff = offset;
            if (isbigend) {
                tmpoff = intype.getSize() - 1 - offset;
            }
            return isSubpieceCast(outtype, intype, tmpoff);
        }

        public override bool isSextCast(Datatype outtype, Datatype intype)
        {
            type_metatype metaout = outtype.getMetatype();
            if (metaout != type_metatype.TYPE_UINT && metaout != type_metatype.TYPE_INT) {
                return false;
            }
            type_metatype metain = intype.getMetatype();
            // Casting to larger storage always extends based on signedness of the input data-type
            // So the input must be SIGNED in order to treat SEXT as a cast
            return ((metain == type_metatype.TYPE_INT) || (metain = type_metatype.TYPE_BOOL));
        }

        public override bool isZextCast(Datatype outtype, Datatype intype)
        {
            type_metatype metaout = outtype.getMetatype();
            if (metaout != type_metatype.TYPE_UINT && metaout != type_metatype.TYPE_INT) {
                return false;
            }
            type_metatype metain = intype.getMetatype();
            // Casting to larger storage always extends based on signedness of the input data-type
            // So the input must be UNSIGNED in order to treat ZEXT as a cast
            return ((metain == type_metatype.TYPE_UINT) || (metain == type_metatype.TYPE_BOOL));
        }
    }
}
