using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief Casting strategies that are specific to the Java language
    /// This is nearly identical to the strategy for C, but there is some change to account
    /// for the way object references are encoded as pointer data-types within the
    /// decompiler's data-type system.
    internal class CastStrategyJava : CastStrategyC
    {
        public override Datatype? castStandard(Datatype reqtype, Datatype curtype, bool care_uint_int,
            bool care_ptr_uint)
        {
            if (curtype == reqtype) {
                // Types are equal, no cast required
                return null;
            }
            Datatype reqbase = reqtype;
            Datatype curbase = curtype;
            if ((reqbase.getMetatype() == type_metatype.TYPE_PTR) || (curbase.getMetatype() == type_metatype.TYPE_PTR)) {
                // There must be explicit cast op between objects, so assume no cast necessary
                return null;
            }

            if ((reqbase.getMetatype() == type_metatype.TYPE_VOID) || (curtype.getMetatype() == type_metatype.TYPE_VOID)) {
                // Don't cast from or to VOID
                return null;
            }
            if (reqbase.getSize() != curbase.getSize()) {
                // Always cast change in size
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

        public override bool isZextCast(Datatype outtype, Datatype intype)
        {
            type_metatype outmeta = outtype.getMetatype();
            if ((outmeta != type_metatype.TYPE_INT) && (outmeta != type_metatype.TYPE_UINT) && (outmeta != type_metatype.TYPE_BOOL)) {
                return false;
            }
            type_metatype inmeta = intype.getMetatype();
            if ((inmeta != type_metatype.TYPE_INT) && (inmeta != type_metatype.TYPE_UINT) && (inmeta != type_metatype.TYPE_BOOL)) {
                // Non-integer types, print functional ZEXT
                return false;
            }
            if ((intype.getSize() == 2) && (!intype.isCharPrint())) {
                // cast is not zext for short
                return false;
            }
            if ((intype.getSize() == 1) && (inmeta == type_metatype.TYPE_INT)) {
                // cast is not zext for byte
                return false;
            }
            return (intype.getSize() < 4);
        }
    }
}
