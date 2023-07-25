using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.CORE
{
    /// \brief Special AddrSpace for representing constants during analysis.
    /// The underlying RTL (See PcodeOp) represents all data in terms of
    /// an Address, which is made up of an AddrSpace and offset pair.
    /// In order to represent constants in the semantics of the RTL,
    /// there is a special \e constant address space.  An \e offset
    /// within the address space encodes the actual constant represented
    /// by the pair.  I.e. the pair (\b const,4) represents the constant
    /// \b 4 within the RTL.  The \e size of the ConstantSpace has
    /// no meaning, as we always want to be able to represent an arbitrarily
    /// large constant.  In practice, the size of a constant is limited
    /// by the offset field of an Address.
    public class ConstantSpace : AddrSpace
    {
        /// Reserved name for the address space
        public const string NAME = "const";
        /// Reserved index for constant space
        public const int INDEX = 0;
        // WARNING : This is a trick. The unique identifier field member is used during
        // Address instances creation to simulate an offset in the ConstantSpace which
        // is guaranteed to be unique.
        internal readonly ulong _uniqueId;
        private static ulong NextUniqueId = 1;

        /// Only constructor
        /// This constructs the unique constant space
        /// By convention, the name is always "const" and the index
        /// is always 0.
        /// \param m is the associated address space manager
        /// \param t is the associated processor translator
        public ConstantSpace(AddrSpaceManager m, Translate t)
            : base(m, t, spacetype.IPTR_CONSTANT, NAME, sizeof(ulong), 1, INDEX, 0, 0)
        {
            lock (typeof(AddrSpace)) {
                if (ulong.MaxValue == (_uniqueId = NextUniqueId++)) {
                    throw new OverflowException();
                }
            }
            clearFlags(Properties.heritaged | Properties.does_deadcode
                | Properties.big_endian);
            //if (HOST_ENDIAN == 1) {
            //    // Endianness always matches host
            //    setFlags(Properties.big_endian);
            //}
        }

        internal override bool IsConstantSpace => true;

        public override int overlapJoin(ulong offset, int size, AddrSpace pointSpace,
            ulong pointOff, int pointSkip)
        {
            return -1;
        }

        /// Constants are always printed as hexidecimal values in
        /// the debugger and console dumps
        public override void printRaw(StreamWriter s, ulong offset)
        {
            s.Write($"0x{offset:X}");
        }

        /// The ConstantSpace should never be explicitly saved as it is
        /// always built automatically
        public override void saveXml(StreamWriter s)
        {
            throw new LowlevelError("Should never save the constant space as XML");
        }

        /// As the ConstantSpace is never saved, it should never get
        /// decoded either.
        public virtual void decode(ref Decoder decoder)
        {
            throw new LowlevelError("Should never decode the constant space");
        }
    }
}
