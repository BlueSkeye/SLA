using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// Specializations of the core meta-types.  Each enumeration is associated with a specific #type_metatype.
    /// Ordering is important: The lower the number, the more \b specific the data-type, affecting propagation.
    internal enum sub_metatype
    {
        /// Compare as a type_metatype.TYPE_VOID
        SUB_VOID = 22,
        /// Compare as a type_metatype.TYPE_SPACEBASE
        SUB_SPACEBASE = 21,
        /// Compare as a type_metatype.TYPE_UNKNOWN
        SUB_UNKNOWN = 20,
        /// Compare as type_metatype.TYPE_PARTIALSTRUCT
        SUB_PARTIALSTRUCT = 19,
        /// Signed 1-byte character, sub-type of type_metatype.TYPE_INT
        SUB_INT_CHAR = 18,
        /// Unsigned 1-byte character, sub-type of type_metatype.TYPE_UINT
        SUB_UINT_CHAR = 17,
        /// Compare as a plain type_metatype.TYPE_INT
        SUB_INT_PLAIN = 16,
        /// Compare as a plain type_metatype.TYPE_UINT
        SUB_UINT_PLAIN = 15,
        /// Signed enum, sub-type of type_metatype.TYPE_INT
        SUB_INT_ENUM = 14,
        /// Unsigned enum, sub-type of type_metatype.TYPE_UINT
        SUB_UINT_ENUM = 13,
        /// Signed wide character, sub-type of type_metatype.TYPE_INT
        SUB_INT_UNICODE = 12,
        /// Unsigned wide character, sub-type of type_metatype.TYPE_UINT
        SUB_UINT_UNICODE = 11,
        /// Compare as type_metatype.TYPE_BOOL
        SUB_BOOL = 10,
        /// Compare as type_metatype.TYPE_CODE
        SUB_CODE = 9,
        /// Compare as type_metatype.TYPE_FLOAT
        SUB_FLOAT = 8,
        /// Pointer to unknown field of struct, sub-type of type_metatype.TYPE_PTR
        SUB_PTRREL_UNK = 7,
        /// Compare as type_metatype.TYPE_PTR
        SUB_PTR = 6,
        /// Pointer relative to another data-type, sub-type of type_metatype.TYPE_PTR
        SUB_PTRREL = 5,
        /// Pointer into struct, sub-type of type_metatype.TYPE_PTR
        SUB_PTR_STRUCT = 4,
        /// Compare as type_metatype.TYPE_ARRAY
        SUB_ARRAY = 3,
        /// Compare as type_metatype.TYPE_STRUCT
        SUB_STRUCT = 2,
        /// Compare as type_metatype.TYPE_UNION
        SUB_UNION = 1,
        /// Compare as a type_metatype.TYPE_PARTIALUNION
        SUB_PARTIALUNION = 0
    }
}
