using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// The core meta-types supported by the decompiler. These are sizeless templates
    /// for the elements making up the type algebra.  Index is important for Datatype::base2sub array.
    internal enum type_metatype
    {
        /// Standard "void" type, absence of type
        TYPE_VOID = 14,
        /// Placeholder for symbol/type look-up calculations
        TYPE_SPACEBASE = 13,
        /// An unknown low-level type. Treated as an unsigned integer.
        TYPE_UNKNOWN = 12,
        /// Signed integer. Signed is considered less specific than unsigned in C
        TYPE_INT = 11,
        /// Unsigned integer
        /// Boolean
        TYPE_UINT = 10,
        /// Data is actual executable code
        TYPE_CODE = 8,
        /// Floating-point
        TYPE_FLOAT = 7,

        /// Pointer data-type
        TYPE_PTR = 6,
        /// Pointer relative to another data-type (specialization of TYPE_PTR)
        TYPE_PTRREL = 5,
        /// Array data-type, made up of a sequence of "element" datatype
        TYPE_ARRAY = 4,
        /// Structure data-type, made up of component datatypes
        TYPE_STRUCT = 3,
        /// An overlapping union of multiple datatypes
        TYPE_UNION = 2,
        /// Part of a structure, stored separately from the whole
        TYPE_PARTIALSTRUCT = 1,
        /// Part of a union
        TYPE_PARTIALUNION = 0
    }
}
