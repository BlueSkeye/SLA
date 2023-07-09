using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ghidra
{
    /// \brief Fundemental address space types
    /// Every address space must be one of the following core types
    public enum spacetype
    {
        IPTR_CONSTANT = 0,         ///< Special space to represent constants
        IPTR_PROCESSOR = 1,        ///< Normal spaces modelled by processor
        IPTR_SPACEBASE = 2,        ///< addresses = offsets off of base register
        IPTR_INTERNAL = 3,         ///< Internally managed temporary space
        IPTR_FSPEC = 4,        ///< Special internal FuncCallSpecs reference
        IPTR_IOP = 5,                ///< Special internal PcodeOp reference
        IPTR_JOIN = 6              ///< Special virtual space to represent split variables
    }
}
