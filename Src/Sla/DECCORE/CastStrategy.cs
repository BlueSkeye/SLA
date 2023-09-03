using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief A strategy for applying type casts
    /// A \e cast operation in C or other languages masks a variety of possible low-level conversions,
    /// such as extensions, truncations, integer to floating-point, etc. On top of this, languages allow
    /// many of these types of operations to be \e implied in the source code, with no explicit token
    /// representing the conversion.  Conversions happen automatically for things like \e integer \e promotion,
    /// between different sizes (of integers), and between signed and unsigned data-type variants.
    ///
    /// This class is the API for making four kinds of decisions:
    ///   - Do we need a cast operator for a given assignment
    ///   - Does the given conversion operation need to be represented as a cast
    ///   - Does the given extension or comparison match with the expected level of integer promotion
    ///   - What data-type is produced by a particular integer arithmetic operation
    internal abstract class CastStrategy
    {
        /// \brief Types of integer promotion
        /// For many languages, small integers are automatically \e promoted to a standard size. The decompiler
        /// describes how an expression is or will be affected by integer promotion, using these codes
        public enum IntPromotionCode
        {
            /// There is no integer promotion
            NO_PROMOTION = -1,
            /// The type of integer promotion cannot be determined
            UNKNOWN_PROMOTION = 0,
            /// The value is promoted using unsigned extension
            UNSIGNED_EXTENSION = 1,
            /// The value is promoted using signed extension
            SIGNED_EXTENSION = 2,
            /// The value is promoted using either signed or unsigned extension
            EITHER_EXTENSION = 3
        }

        /// Type factory associated with the Architecture
        protected TypeFactory tlst;
        /// Size of \b int data-type, (size that integers get promoted to)
        protected int promoteSize;

        /// Constructor
        public CastStrategy()
        {
        }

        /// Establish the data-type factory
        /// Sets the TypeFactory used to produce data-types for the arithmeticOutputStandard() method
        /// \param t is the TypeFactory
        public void setTypeFactory(TypeFactory t)
        {
            tlst = t;
            promoteSize = tlst.getSizeOfInt();
        }

        /// Destructor
        ~CastStrategy()
        {
        }

        /// \brief Decide on integer promotion by examining just local properties of the given Varnode
        /// \param vn is the given Varnode
        /// \param op is the PcodeOp reading the Varnode
        /// \return an IntPromotionCode (excluding NO_PROMOTION)
        public abstract IntPromotionCode localExtensionType(Varnode vn, PcodeOp op);

        /// \brief Calculate the integer promotion code of a given Varnode
        /// Recursively examine the expression defining the Varnode as necessary
        /// \param vn is the given Varnode
        /// \return the IntPromotionCode
        public abstract IntPromotionCode intPromotionType(Varnode vn);

        /// \brief Check if integer promotion forces a cast for the given comparison op and slot
        /// Compute to what level the given slot has seen integer promotion and if
        /// a cast is required before the comparison operator makes sense.
        /// \param op is the given comparison operator
        /// \param slot is the input slot being tested
        /// \return \b true if a cast is required before comparing
        public abstract bool checkIntPromotionForCompare(PcodeOp op, int slot);

        /// \brief Check if integer promotion forces a cast for the input to the given extension.
        /// Compute to what level the given slot has seen integer promotion and if
        /// a cast is required before the extension operator makes sense.
        /// \param op is the given extension operator INT_ZEXT or INT_SEXT
        /// \return \b true if a cast is required before extending
        public abstract bool checkIntPromotionForExtension(PcodeOp op);

        /// \brief Is the given ZEXT/SEXT cast implied by the expression its in?
        /// We've already determined that the given ZEXT or SEXT op can be viewed as a natural \e cast operation.
        /// Determine if the cast is implied by the expression its and doesn't need to be printed.
        /// \param op is the given ZEXT or SEXT PcodeOp
        /// \param readOp is the PcodeOp consuming the output of the extensions (or null)
        /// \return \b true if the op as a cast does not need to be printed
        public abstract bool isExtensionCastImplied(PcodeOp op, PcodeOp readOp);

        /// \brief Does there need to be a visible cast between the given data-types
        /// The cast is from a \e current data-type to an \e expected data-type. NULL is returned
        /// if no cast is required, otherwise the data-type to cast to (usually the expected data-type)
        /// is returned.
        /// \param reqtype is the \e expected data-type
        /// \param curtype is the \e current data-type
        /// \param care_uint_int is \b true if we care about a change in signedness
        /// \param care_ptr_uint is \b true if we care about conversions between pointers and unsigned values
        /// \return NULL to indicate no cast, or the data-type to cast to
        public abstract Datatype? castStandard(Datatype reqtype, Datatype curtype,
            bool care_uint_int, bool care_ptr_uint);

        /// \brief What is the output data-type produced by the given integer arithmetic operation
        /// \param op is the given operation
        /// \return the output data-type
        public abstract Datatype arithmeticOutputStandard(PcodeOp op);

        /// \brief Is truncating an input data-type, producing an output data-type, considered a cast
        /// Data-types must be provided from the input and output of a SUBPIECE operation.
        /// \param outtype is the output data-type
        /// \param intype is the input data-type
        /// \param offset is number of bytes truncated by the SUBPIECE
        /// \return \b true if the SUBPIECE should be represented as a cast
        public abstract bool isSubpieceCast(Datatype outtype, Datatype intype, uint offset);

        /// \brief Is the given data-type truncation considered a cast, given endianess concerns.
        /// This is equivalent to isSubpieceCast() but where the truncation is accomplished by pulling
        /// bytes directly out of memory.  We assume the input data-type is layed down in memory, and
        /// we pull the output value starting at a given byte offset.
        /// \param outtype is the output data-type
        /// \param intype is the input data-type
        /// \param offset is the given byte offset (into the input memory)
        /// \param isbigend is \b true if the address space holding the memory is big endian.
        /// \return \b true if the truncation should be represented as a cast
        public abstract bool isSubpieceCastEndian(Datatype outtype, Datatype intype, uint offset,
            bool isbigend);

        /// \brief Is sign-extending an input data-type, producing an output data-type, considered a cast
        /// Data-types must be provided from the input and output of an INT_SEXT operation.
        /// \param outtype is the output data-type
        /// \param intype is the input data-type
        /// \return \b true if the INT_SEXT should be represented as a cast
        public abstract bool isSextCast(Datatype outtype, Datatype intype);

        /// \brief Is zero-extending an input data-type, producing an output data-type, considered a cast
        /// Data-types must be provided from the input and output of an INT_ZEXT operation.
        /// \param outtype is the output data-type
        /// \param intype is the input data-type
        /// \return \b true if the INT_ZEXT should be represented as a cast
        public abstract bool isZextCast(Datatype outtype, Datatype intype);

        /// \brief Check if a constant input should be explicitly labeled as an \e unsigned token
        /// Many languages can mark an integer constant as explicitly \e unsigned. When
        /// the decompiler is deciding on \e cast operations, this is one of the checks
        /// it performs.  This method checks if the indicated input is an
        /// integer constant that needs to be coerced (as a source token) into being unsigned.
        /// If this is \b true, the input Varnode is marked for printing as explicitly \e unsigned.
        /// \param op is the PcodeOp taking the value as input
        /// \param slot is the input slot of the value
        /// \return \b true if the Varnode gets marked for printing
        public bool markExplicitUnsigned(PcodeOp op, int slot)
        {
            TypeOp opcode = op.getOpcode();
            if (!opcode.inheritsSign()) {
                return false;
            }
            bool inheritsFirstParamOnly = opcode.inheritsSignFirstParamOnly();
            if ((slot == 1) && inheritsFirstParamOnly) {
                return false;
            }
            Varnode vn = op.getIn(slot);
            if (!vn.isConstant()) {
                return false;
            }
            Datatype dt = vn.getHighTypeReadFacing(op);
            type_metatype meta = dt.getMetatype();
            if ((meta != type_metatype.TYPE_UINT) && (meta != type_metatype.TYPE_UNKNOWN)) {
                return false;
            }
            if (dt.isCharPrint()) {
                return false;
            }
            if (dt.isEnumType()) {
                return false;
            }
            if ((op.numInput() == 2) && !inheritsFirstParamOnly) {
                Varnode firstvn = op.getIn(1 - slot);
                meta = firstvn.getHighTypeReadFacing(op).getMetatype();
                if ((meta == type_metatype.TYPE_UINT) || (meta == type_metatype.TYPE_UNKNOWN)){
                    // Other side of the operation will force the unsigned
                    return false;
                }
            }
            // Check if type is going to get forced anyway
            Varnode outvn = op.getOut();
            if (outvn != null) {
                if (outvn.isExplicit()) {
                    return false;
                }
                PcodeOp lone = outvn.loneDescend();
                if (lone != null) {
                    if (!lone.getOpcode().inheritsSign()) {
                        return false;
                    }
                }
            }
            vn.setUnsignedPrint();
            return true;
        }

        /// \brief Check is a constant input should be explicitly labeled as a \e long integer
        /// token
        /// This method checks if the indicated input is an integer constant that needs to be coerced
        /// (as a source token) into a data-type that is larger than the base integer. If this is \b true,
        /// the input Varnode is marked for printing as explicitly a larger integer (typically \e long).
        /// \param op is the PcodeOp taking the value as input
        /// \param slot is the input slot of the value
        /// \return \b true if the Varnode gets marked for printing
        public bool markExplicitLongSize(PcodeOp op, int slot)
        {
            if (!op.getOpcode().isShiftOp()) {
                return false;
            }
            if (slot != 0) {
                return false;
            }
            Varnode vn = op.getIn(slot);
            if (!vn.isConstant()) {
                return false;
            }
            if (vn.getSize() <= promoteSize) {
                return false;
            }
            Datatype dt = vn.getHigh().getType();
            type_metatype meta = dt.getMetatype();
            if ((meta != type_metatype.TYPE_UINT) && (meta != type_metatype.TYPE_INT) && (meta != type_metatype.TYPE_UNKNOWN)) {
                return false;
            }
            ulong off = vn.getOffset();
            if (meta == type_metatype.TYPE_INT && Globals.signbit_negative(off, vn.getSize())) {
                off = Globals.uintb_negate(off, vn.getSize());
                int bit = Globals.mostsigbit_set(off);
                if (bit >= promoteSize * 8 - 1) {
                    return false;
                }
            }
            else {
                int bit = Globals.mostsigbit_set(off);
                // If integer is big enough, it naturally becomes a long
                if (bit >= promoteSize * 8) {
                    return false;
                }
            }
            vn.setLongPrint();
            return true;
        }

        /// \brief For the given PcodeOp, does it matter if a constant operand is presented as a character or integer
        /// In most languages, character constants are promoted to integers as a matter of course, so it
        /// doesn't matter if the constant is represented as an integer (a string of digits) or a character
        /// (surrounded by quotes). But its possible that a particular operator does care. If the operator
        /// needs an explicit character representation for an operand with a character data-type, return \b true.
        /// \param vn is the constant with character data-type
        /// \param op is the given PcodeOp which reads the constant (may be null)
        /// \return \b true if the constant must be represented as an explicit character
        public bool caresAboutCharRepresentation(Varnode vn, PcodeOp op)
        {
            return false;
        }
    }
}
