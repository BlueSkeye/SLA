/* ###
 * IP: GHIDRA
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 * 
 *      http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
//#include "opcodes.hh"
//#include "types.h"

namespace Sla.CORE {

    /// \brief The op-code defining a specific p-code operation (PcodeOp)
    ///
    /// These break up into categories:
    ///   - Branching operations
    ///   - Load and Store
    ///   - Comparison operations
    ///   - Arithmetic operations
    ///   - Logical operations
    ///   - Extension and truncation operations
    public enum OpCode
    {
        /// Copy one operand to another
        CPUI_COPY = 1,
        /// Load from a pointer into a specified address space
        CPUI_LOAD = 2,
        /// Store at a pointer into a specified address space
        CPUI_STORE = 3,

        /// Always branch
        CPUI_BRANCH = 4,
        /// Conditional branch
        CPUI_CBRANCH = 5,
        /// Indirect branch (jumptable)
        CPUI_BRANCHIND = 6,

        /// Call to an absolute address
        CPUI_CALL = 7,
        /// Call through an indirect address
        CPUI_CALLIND = 8,
        /// User-defined operation
        CPUI_CALLOTHER = 9,
        /// Return from subroutine
        CPUI_RETURN = 10,

        // Integer/bit operations

        CPUI_INT_EQUAL = 11,        ///< Integer comparison, equality (==)
        CPUI_INT_NOTEQUAL = 12, ///< Integer comparison, in-equality (!=)
        CPUI_INT_SLESS = 13,        ///< Integer comparison, signed less-than (<)
        CPUI_INT_SLESSEQUAL = 14,   ///< Integer comparison, signed less-than-or-equal (<=)
        CPUI_INT_LESS = 15,     ///< Integer comparison, unsigned less-than (<)
        // This also indicates a borrow on unsigned substraction
        CPUI_INT_LESSEQUAL = 16,    ///< Integer comparison, unsigned less-than-or-equal (<=)
        CPUI_INT_ZEXT = 17,     ///< Zero extension
        CPUI_INT_SEXT = 18,     ///< Sign extension
        CPUI_INT_ADD = 19,      ///< Addition, signed or unsigned (+)
        CPUI_INT_SUB = 20,      ///< Subtraction, signed or unsigned (-)
        CPUI_INT_CARRY = 21,        ///< Test for unsigned carry
        CPUI_INT_SCARRY = 22,       ///< Test for signed carry
        CPUI_INT_SBORROW = 23,  ///< Test for signed borrow
        CPUI_INT_2COMP = 24,        ///< Twos complement
        CPUI_INT_NEGATE = 25,       ///< Logical/bitwise negation (~)
        CPUI_INT_XOR = 26,      ///< Logical/bitwise exclusive-or (^)
        CPUI_INT_AND = 27,      ///< Logical/bitwise and (&)
        CPUI_INT_OR = 28,       ///< Logical/bitwise or (|)
        CPUI_INT_LEFT = 29,     ///< Left shift (<<)
        CPUI_INT_RIGHT = 30,        ///< Right shift, logical (>>)
        CPUI_INT_SRIGHT = 31,       ///< Right shift, arithmetic (>>)
        CPUI_INT_MULT = 32,     ///< Integer multiplication, signed and unsigned (*)
        CPUI_INT_DIV = 33,      ///< Integer division, unsigned (/)
        CPUI_INT_SDIV = 34,     ///< Integer division, signed (/)
        CPUI_INT_REM = 35,      ///< Remainder/modulo, unsigned (%)
        CPUI_INT_SREM = 36,     ///< Remainder/modulo, signed (%)

        CPUI_BOOL_NEGATE = 37,  ///< Boolean negate (!)
        CPUI_BOOL_XOR = 38,     ///< Boolean exclusive-or (^^)
        CPUI_BOOL_AND = 39,     ///< Boolean and (&&)
        CPUI_BOOL_OR = 40,      ///< Boolean or (||)

        // Floating point operations

        CPUI_FLOAT_EQUAL = 41,        ///< Floating-point comparison, equality (==)
        CPUI_FLOAT_NOTEQUAL = 42,   ///< Floating-point comparison, in-equality (!=)
        CPUI_FLOAT_LESS = 43,       ///< Floating-point comparison, less-than (<)
        CPUI_FLOAT_LESSEQUAL = 44,  ///< Floating-point comparison, less-than-or-equal (<=)
        // Slot 45 is currently unused
        CPUI_FLOAT_NAN = 46,            ///< Not-a-number test (NaN)

        CPUI_FLOAT_ADD = 47,          ///< Floating-point addition (+)
        CPUI_FLOAT_DIV = 48,          ///< Floating-point division (/)
        CPUI_FLOAT_MULT = 49,         ///< Floating-point multiplication (*)
        CPUI_FLOAT_SUB = 50,          ///< Floating-point subtraction (-)
        CPUI_FLOAT_NEG = 51,          ///< Floating-point negation (-)
        CPUI_FLOAT_ABS = 52,          ///< Floating-point absolute value (abs)
        CPUI_FLOAT_SQRT = 53,         ///< Floating-point square root (sqrt)

        CPUI_FLOAT_INT2FLOAT = 54,    ///< Convert an integer to a floating-point
        CPUI_FLOAT_FLOAT2FLOAT = 55,  ///< Convert between different floating-point sizes
        CPUI_FLOAT_TRUNC = 56,        ///< Round towards zero
        CPUI_FLOAT_CEIL = 57,         ///< Round towards +infinity
        CPUI_FLOAT_FLOOR = 58,        ///< Round towards -infinity
        CPUI_FLOAT_ROUND = 59,  ///< Round towards nearest

        // Internal opcodes for simplification. Not
        // typically generated in a direct translation.

        // Data-flow operations
        CPUI_MULTIEQUAL = 60,       ///< Phi-node operator
        CPUI_INDIRECT = 61,     ///< Copy with an indirect effect
        CPUI_PIECE = 62,        ///< Concatenate
        CPUI_SUBPIECE = 63,     ///< Truncate

        CPUI_CAST = 64,     ///< Cast from one data-type to another
        CPUI_PTRADD = 65,       ///< Index into an array ([])
        CPUI_PTRSUB = 66,       ///< Drill down to a sub-field  (->)
        CPUI_SEGMENTOP = 67,        ///< Look-up a \e segmented address
        CPUI_CPOOLREF = 68,     ///< Recover a value from the \e constant \e pool
        CPUI_NEW = 69,      ///< Allocate a new object (new)
        CPUI_INSERT = 70,       ///< Insert a bit-range
        CPUI_EXTRACT = 71,      ///< Extract a bit-range
        CPUI_POPCOUNT = 72,     ///< Count the 1-bits
        /// Count the leading 0-bits
        CPUI_LZCOUNT = 73,

        /// Value indicating the end of the op-code values
        CPUI_MAX = 74
    };

    public static partial class Globals
    {
        /// \brief Names of operations associated with their opcode number
        ///
        /// Some of the names have been replaced with special placeholder
        /// ops for the sleigh compiler and interpreter these are as follows:
        ///  -  MULTIEQUAL = BUILD
        ///  -  INDIRECT   = DELAY_SLOT
        ///  -  PTRADD     = LABEL
        ///  -  PTRSUB     = CROSSBUILD
        public static readonly string[] opcode_name = {
            "BLANK", "COPY", "LOAD", "STORE",
            "BRANCH", "CBRANCH", "BRANCHIND", "CALL",
            "CALLIND", "CALLOTHER", "RETURN", "INT_EQUAL",
            "INT_NOTEQUAL", "INT_SLESS", "INT_SLESSEQUAL", "INT_LESS",
            "INT_LESSEQUAL", "INT_ZEXT", "INT_SEXT", "INT_ADD",
            "INT_SUB", "INT_CARRY", "INT_SCARRY", "INT_SBORROW",
            "INT_2COMP", "INT_NEGATE", "INT_XOR", "INT_AND",
            "INT_OR", "INT_LEFT", "INT_RIGHT", "INT_SRIGHT",
            "INT_MULT", "INT_DIV", "INT_SDIV", "INT_REM",
            "INT_SREM", "BOOL_NEGATE", "BOOL_XOR", "BOOL_AND",
            "BOOL_OR", "FLOAT_EQUAL", "FLOAT_NOTEQUAL", "FLOAT_LESS",
            "FLOAT_LESSEQUAL", "UNUSED1", "FLOAT_NAN", "FLOAT_ADD",
            "FLOAT_DIV", "FLOAT_MULT", "FLOAT_SUB", "FLOAT_NEG",
            "FLOAT_ABS", "FLOAT_SQRT", "INT2FLOAT", "FLOAT2FLOAT",
            "TRUNC", "CEIL", "FLOOR", "ROUND",
            "BUILD", "DELAY_SLOT", "PIECE", "SUBPIECE", "CAST",
            "LABEL", "CROSSBUILD", "SEGMENTOP", "CPOOLREF", "NEW",
            "INSERT", "EXTRACT", "POPCOUNT", "LZCOUNT"
        };

        public static readonly int[] opcode_indices = {
            0, 39, 37, 40, 38,  4,  6, 60,  7,  8,  9, 64,  5, 57,  1, 68, 66,
            61, 71, 55, 52, 47, 48, 41, 43, 44, 49, 46, 51, 42, 53, 50, 58, 70,
            54, 24, 19, 27, 21, 33, 11, 29, 15, 16, 32, 25, 12, 28, 35, 30,
            23, 22, 34, 18, 13, 14, 36, 31, 20, 26, 17, 65,  2, 73, 69, 62, 72, 10, 59,
            67,  3, 63, 56, 45
        };

        /// \param opc is an OpCode value
        /// \return the name of the operation as a string
        public static string get_opname(OpCode opc)
        {
            return opcode_name[(int)opc];
        }

        /// \param nm is the name of an operation
        /// \return the corresponding OpCode value
        public static OpCode get_opcode(ref string nm)
        {
            int min = 1;           // Don't include BLANK
            int max = (int)OpCode.CPUI_MAX - 1;
            int cur, ind;

            while (min <= max) {
                // Binary search
                cur = (min + max) / 2;
                // Get opcode in cur's sort slot
                ind = opcode_indices[cur];
                int comparisonResult = string.Compare(opcode_name[ind], nm);
                if (comparisonResult < 0) {
                    // Everything equal or below cur is less
                    min = cur + 1;
                }
                else if (comparisonResult > 0) {
                    // Everything equal or above cur is greater
                    max = cur - 1;
                }
                else {
                    // Found the match
                    return (OpCode)ind;
                }
            }
            // Name isn't an op
            return (OpCode)0;
        }

        /// Every comparison operation has a complementary form that produces
        /// the opposite output on the same inputs. Set \b reorder to true if
        /// the complimentary operation involves reordering the input parameters.
        /// \param opc is the OpCode to complement
        /// \param reorder is set to \b true if the inputs need to be reordered
        /// \return the complementary OpCode or CPUI_MAX if not given a comparison operation
        public static OpCode get_booleanflip(OpCode opc, ref bool reorder)
        {
            switch (opc) {
                case OpCode.CPUI_INT_EQUAL:
                    reorder = false;
                    return OpCode.CPUI_INT_NOTEQUAL;
                case OpCode.CPUI_INT_NOTEQUAL:
                    reorder = false;
                    return OpCode.CPUI_INT_EQUAL;
                case OpCode.CPUI_INT_SLESS:
                    reorder = true;
                    return OpCode.CPUI_INT_SLESSEQUAL;
                case OpCode.CPUI_INT_SLESSEQUAL:
                    reorder = true;
                    return OpCode.CPUI_INT_SLESS;
                case OpCode.CPUI_INT_LESS:
                    reorder = true;
                    return OpCode.CPUI_INT_LESSEQUAL;
                case OpCode.CPUI_INT_LESSEQUAL:
                    reorder = true;
                    return OpCode.CPUI_INT_LESS;
                case OpCode.CPUI_BOOL_NEGATE:
                    reorder = false;
                    return OpCode.CPUI_COPY;
                case OpCode.CPUI_FLOAT_EQUAL:
                    reorder = false;
                    return OpCode.CPUI_FLOAT_NOTEQUAL;
                case OpCode.CPUI_FLOAT_NOTEQUAL:
                    reorder = false;
                    return OpCode.CPUI_FLOAT_EQUAL;
                case OpCode.CPUI_FLOAT_LESS:
                    reorder = true;
                    return OpCode.CPUI_FLOAT_LESSEQUAL;
                case OpCode.CPUI_FLOAT_LESSEQUAL:
                    reorder = true;
                    return OpCode.CPUI_FLOAT_LESS;
                default:
                    return OpCode.CPUI_MAX;
            }
        }
    }
}
