using Sla.CORE;
using Sla.DECCORE;
using Sla.SLACOMP;
using Sla.SLEIGH;
using static Sla.EXTRA.UnifyDatatype;
using System;

namespace Sla
{
    internal static partial class Globals
    {
        // #if defined (__x86_64__) || defined (__i386__)
        internal const int HOST_ENDIAN = 0;

#if UINTB4
        ulong[] uintbmasks = new ulong[] {
            0,
            0xff,
            0xffff,
            0xffffff,
            0xffffffff,
            0xffffffff,
            0xffffffff,
            0xffffffff,
            0xffffffff
        };
#else
        internal static ulong[] uintbmasks = new ulong[] {
            0,
            0xff,
            0xffff,
            0xffffff,
            0xffffffff,
            0xffffffffffL,
            0xffffffffffffL,
            0xffffffffffffffL,
            0xffffffffffffffffL
        };
#endif

        /// \param size is the desired size in bytes
        /// \return a value appropriate for masking off the first \e size bytes
        public static ulong calc_mask(uint size)
        {
            return uintbmasks[(8 > size) ? size : 8];
        }

        /// Perform a OpCode.CPUI_INT_RIGHT on the given val
        /// \param val is the value to shift
        /// \param sa is the number of bits to shift
        /// \return the shifted value
        public static ulong pcode_right(ulong val, int sa)
        {
            return (sa >= 8 * sizeof(ulong)) ? 0 : val >> sa;
        }

        /// Perform a OpCode.CPUI_INT_LEFT on the given val
        /// \param val is the value to shift
        /// \param sa is the number of bits to shift
        /// \return the shifted value
        public static ulong pcode_left(ulong val, int sa)
        {
            return (sa >= 8 * sizeof(ulong)) ? 0 : val << sa;
        }

        /// \brief Calculate smallest mask that covers the given value
        /// Calculcate a mask that covers either the least significant byte, ushort, uint, or ulong,
        /// whatever is smallest.
        /// \param val is the given value
        /// \return the minimal mask
        public static ulong minimalmask(ulong val)
        {
            if (val > 0xffffffff) {
                return ulong.MaxValue;
            }
            if (val > 0xffff) {
                return uint.MaxValue;
            }
            if (val > 0xff) {
                return ushort.MaxValue;
            }
            return byte.MaxValue;
        }

        //public static StreamWriter operator <<(StreamWriter s, SeqNum sq)
        //{
        //    sq.pc.printRaw(s);
        //    s.Write(':');
        //    s.Write(sq.uniq);
        //    return s;
        //}

        ///// This allows an Address to be written to a stream using the standard '<<'
        ///// operator. This is a wrapper for the printRaw method and is intended for
        ///// debugging and console mode uses.
        ///// \param s is the stream being written to
        ///// \param addr is the Address to write
        ///// \return the output stream
        //public static StreamWriter operator <<(StreamWriter s, Address addr)
        //{
        //    addr.printRaw(s);
        //    return s;
        //}
        /// Convert a type \b meta-type into the string name of the meta-type
        /// \param metatype is the encoded type meta-type
        /// \param res will hold the resulting string
        internal static void metatype2string(type_metatype metatype, out string res)

        {
            switch (metatype) {
                case type_metatype.TYPE_VOID:
                    res = "void";
                    break;
                case type_metatype.TYPE_PTR:
                    res = "ptr";
                    break;
                case type_metatype.TYPE_PTRREL:
                    res = "ptrrel";
                    break;
                case type_metatype.TYPE_ARRAY:
                    res = "array";
                    break;
                case type_metatype.TYPE_PARTIALSTRUCT:
                    res = "partstruct";
                    break;
                case type_metatype.TYPE_PARTIALUNION:
                    res = "partunion";
                    break;
                case type_metatype.TYPE_STRUCT:
                    res = "struct";
                    break;
                case type_metatype.TYPE_UNION:
                    res = "union";
                    break;
                case type_metatype.TYPE_SPACEBASE:
                    res = "spacebase";
                    break;
                case type_metatype.TYPE_UNKNOWN:
                    res = "unknown";
                    break;
                case type_metatype.TYPE_UINT:
                    res = "uint";
                    break;
                case type_metatype.TYPE_INT:
                    res = "int";
                    break;
                case type_metatype.TYPE_BOOL:
                    res = "bool";
                    break;
                case type_metatype.TYPE_CODE:
                    res = "code";
                    break;
                case type_metatype.TYPE_FLOAT:
                    res = "float";
                    break;
                default:
                    throw new LowlevelError("Unknown metatype");
            }
        }

        /// Given a string description of a type \b meta-type. Return the meta-type.
        /// \param metastring is the description of the meta-type
        /// \return the encoded type meta-type
        internal static type_metatype string2metatype(string metastring)
        {
            switch (metastring[0]) {
                case 'p':
                    if (metastring == "ptr")
                        return type_metatype.TYPE_PTR;
                    else if (metastring == "ptrrel")
                        return type_metatype.TYPE_PTRREL;
                    else if (metastring == "partunion")
                        return type_metatype.TYPE_PARTIALUNION;
                    else if (metastring == "partstruct")
                        return type_metatype.TYPE_PARTIALSTRUCT;
                    break;
                case 'a':
                    if (metastring == "array")
                        return type_metatype.TYPE_ARRAY;
                    break;
                case 's':
                    if (metastring == "struct")
                        return type_metatype.TYPE_STRUCT;
                    if (metastring == "spacebase")
                        return type_metatype.TYPE_SPACEBASE;
                    break;
                case 'u':
                    if (metastring == "unknown")
                        return type_metatype.TYPE_UNKNOWN;
                    else if (metastring == "uint")
                        return type_metatype.TYPE_UINT;
                    else if (metastring == "union")
                        return type_metatype.TYPE_UNION;
                    break;
                case 'i':
                    if (metastring == "int")
                        return type_metatype.TYPE_INT;
                    break;
                case 'f':
                    if (metastring == "float")
                        return type_metatype.TYPE_FLOAT;
                    break;
                case 'b':
                    if (metastring == "bool")
                        return type_metatype.TYPE_BOOL;
                    break;
                case 'c':
                    if (metastring == "code")
                        return type_metatype.TYPE_CODE;
                    break;
                case 'v':
                    if (metastring == "void")
                        return type_metatype.TYPE_VOID;
                    break;
                default:
                    break;
            }
            throw new LowlevelError($"Unknown metatype: {metastring}");
        }

/// Treat the given \b val as a constant of \b size bytes
/// \param val is the given value
/// \param size is the size in bytes
/// \return \b true if the constant (as sized) has its sign bit set
public static bool signbit_negative(ulong val, int size)
        {
            // Return true if signbit is set (negative)
            ulong mask = 0x80;
            mask <<= 8 * (size - 1);
            return ((val & mask) != 0);
        }

        /// Treat the given \b in as a constant of \b size bytes.
        /// Negate this constant keeping the upper bytes zero.
        /// \param in is the given value
        /// \param size is the size in bytes
        /// \return the negation of the sized constant
        public static ulong uintb_negate(ulong @in, int size)
        {
            // Invert bits
            return ((~@in) & Globals.calc_mask((uint)size));
        }

        /// Take the first \b sizein bytes of the given \b in and sign-extend
        /// this to \b sizeout bytes, keeping any more significant bytes zero
        /// \param in is the given value
        /// \param sizein is the size to treat that value as an input
        /// \param sizeout is the size to sign-extend the value to
        /// \return the sign-extended value
        public static ulong sign_extend(ulong @in, int sizein, int sizeout)
        {
            int signbit;
            ulong mask;

            signbit = sizein * 8 - 1;
            @in &= calc_mask((uint)sizein);
            if (sizein >= sizeout) {
                return @in;
            }
            if ((@in >> signbit) != 0) {
                mask = calc_mask((uint)sizeout);
                // Split shift into two pieces
                ulong tmp = mask << signbit;
                // In case, everything is shifted out
                tmp = (tmp << 1) & mask;
                @in |= tmp;
            }
            return @in;
        }

        /// Sign extend \b val starting at \b bit
        /// \param val is a reference to the value to be sign-extended
        /// \param bit is the index of the bit to extend from (0=least significant bit)
        public static void sign_extend(ref long val, int bit)
        {
            long mask = 0;
            mask = (~mask) << bit;
            if (((val >> bit) & 1) != 0) {
                val |= mask;
            }
            else {
                val &= (~mask);
            }
        }

        /// Zero extend \b val starting at \b bit
        /// \param val is a reference to the value to be zero extended
        /// \param bit is the index of the bit to extend from (0=least significant bit)
        public static void zero_extend(ref long val, int bit)
        {
            long mask = 0;
            mask = (~mask) << bit;
            mask <<= 1;
            val &= (~mask);
        }

        /// Swap the least significant \b size bytes in \b val
        /// \param val is a reference to the value to swap
        /// \param size is the number of bytes to swap
        public static void byte_swap(ref long val, int size)
        {
            long res = 0;
            while (size > 0) {
                res <<= 8;
                res |= (val & 0xff);
                val >>= 8;
                size -= 1;
            }
            val = res;
        }

        /// Swap the least significant \b size bytes in \b val
        /// \param val is the value to swap
        /// \param size is the number of bytes to swap
        /// \return the swapped value
        public static ulong byte_swap(ulong val, uint size)
        {
            ulong res = 0;
            while (size > 0) {
                res <<= 8;
                res |= (val & 0xff);
                val >>= 8;
                size -= 1;
            }
            return res;
        }

        /// The least significant bit is index 0.
        /// \param val is the given value
        /// \return the index of the least significant set bit, or -1 if none are set
        public static int leastsigbit_set(ulong val)
        {
            if (val == 0) {
                return -1;
            }
            int res = 0;
            int sz = 4 * sizeof(ulong);
            ulong mask = ulong.MaxValue;
            do {
                mask >>= sz;
                if ((mask & val) == 0) {
                    res += sz;
                    val >>= sz;
                }
                sz >>= 1;
            } while (sz != 0);
            return res;
        }

        /// The least significant bit is index 0.
        /// \param val is the given value
        /// \return the index of the most significant set bit, or -1 if none are set
        public static int mostsigbit_set(ulong val)
        {
            if (val == 0) {
                return -1;
            }
            int res = 8 * sizeof(ulong) - 1;
            int sz = 4 * sizeof(ulong);
            ulong mask = ulong.MaxValue;
            do {
                mask <<= sz;
                if ((mask & val) == 0) {
                    res -= sz;
                    val <<= sz;
                }
                sz >>= 1;
            } while (sz != 0);
            return res;
        }

        /// Count the number (population) bits set.
        /// \param val is the given value
        /// \return the number of one bits
        public static int popcount(ulong val)
        {
            val = (val & 0x5555555555555555L) + ((val >> 1) & 0x5555555555555555L);
            val = (val & 0x3333333333333333L) + ((val >> 2) & 0x3333333333333333L);
            val = (val & 0x0f0f0f0f0f0f0f0fL) + ((val >> 4) & 0x0f0f0f0f0f0f0f0fL);
            val = (val & 0x00ff00ff00ff00ffL) + ((val >> 8) & 0x00ff00ff00ff00ffL);
            val = (val & 0x0000ffff0000ffffL) + ((val >> 16) & 0x0000ffff0000ffffL);
            int res = (int)(val & 0xff);
            res += (int)((val >> 32) & 0xff);
            return res;
        }

        /// Count the number of more significant zero bits before the most significant
        /// one bit in the representation of the given value;
        /// \param val is the given value
        /// \return the number of zero bits
        public static int count_leading_zeros(ulong val)
        {
            if (val == 0) {
                return 8 * sizeof(ulong);
            }
            ulong mask = ulong.MaxValue;
            int maskSize = 4 * sizeof(ulong);
            mask &= (mask << maskSize);
            int bit = 0;
            do {
                if ((mask & val) == 0) {
                    bit += maskSize;
                    maskSize >>= 1;
                    mask |= (mask >> maskSize);
                }
                else {
                    maskSize >>= 1;
                    mask &= (mask << maskSize);
                }
            } while (maskSize != 0);
            return bit;
        }

        /// Return smallest number of form 2^n-1, bigger or equal to the given value
        /// \param val is the given value
        /// \return the mask
        public static ulong coveringmask(ulong val)
        {
            ulong res = val;
            int sz = 1;
            while (sz < 8 * sizeof(ulong)) {
                res = res | (res >> sz);
                sz <<= 1;
            }
            return res;
        }

        /// Treat \b val as a constant of size \b sz.
        /// Scanning across the bits of \b val return the number of transitions (from 0.1 or 1.0)
        /// If there are 2 or less transitions, this is an indication of a bit flag or a mask
        /// \param val is the given value
        /// \param sz is the size to treat the value as
        /// \return the number of transitions
        public static int bit_transitions(ulong val, int sz)
        {
            int res = 0;
            int last = (int)(val & 1);
            int cur;
            for (int i = 1; i < 8 * sz; ++i) {
                val >>= 1;
                cur = (int)(val & 1);
                if (cur != last) {
                    res += 1;
                    last = cur;
                }
                if (val == 0) {
                    break;
                }
            }
            return res;
        }

        /// \brief Multiply 2 unsigned 64-bit values, producing a 128-bit value
        /// TODO: Remove once we import a full multiprecision library.
        /// \param res points to the result array (2 ulong pieces)
        /// \param x is the first 64-bit value
        /// \param y is the second 64-bit value
        public static void mult64to128(ulong[] res, ulong x, ulong y)
        {
            ulong f = x & 0xffffffff;
            ulong e = x >> 32;
            ulong d = y & 0xffffffff;
            ulong c = y >> 32;
            ulong fd = f * d;
            ulong fc = f * c;
            ulong ed = e * d;
            ulong ec = e * c;
            ulong tmp = (fd >> 32) + (fc & 0xffffffff) + (ed & 0xffffffff);
            res[1] = (tmp >> 32) + (fc >> 32) + (ed >> 32) + ec;
            res[0] = (tmp << 32) + (fd & 0xffffffff);
        }

        /// \brief Subtract (in-place) a 128-bit value from a base 128-value
        /// The base value is altered in place.
        /// TODO: Remove once we import a full multiprecision library.
        /// \param a is the base 128-bit value being subtracted from in-place
        /// \param b is the other 128-bit value being subtracted
        public static void unsignedSubtract128(ulong[] a, ulong[] b)
        {
            bool borrow = (a[0] < b[0]);
            a[0] -= b[0];
            a[1] -= b[1];
            if (borrow) {
                a[1] -= 1;
            }
        }

        /// \brief Compare two unsigned 128-bit values
        /// TODO: Remove once we import a full multiprecision library.
        /// Given a first and second value, return -1, 0, or 1 depending on whether the first value
        /// is \e less, \e equal, or \e greater than the second value.
        /// \param a is the first 128-bit value (as an array of 2 ulong elements)
        /// \param b is the second 128-bit value
        /// \return the comparison code
        public static int unsignedCompare128(ulong[] a, ulong[] b)
        {
            if (a[1] != b[1]) {
                return (a[1] < b[1]) ? -1 : 1;
            }
            if (a[0] != b[0]) {
                return (a[0] < b[0]) ? -1 : 1;
            }
            return 0;
        }

        /// \brief Unsigned division of a power of 2 (upto 2^127) by a 64-bit divisor
        /// The result must be less than 2^64. The remainder is calculated.
        /// \param n is the power of 2 for the numerand
        /// \param divisor is the 64-bit divisor
        /// \param q is the passed back 64-bit quotient
        /// \param r is the passed back 64-bit remainder
        /// \return 0 if successful, 1 if result is too big, 2 if divide by 0
        public static int power2Divide(int n, ulong divisor, out ulong q, out ulong r)
        {
            if (divisor == 0) {
                q = r = 0;
                return 2;
            }
            ulong power = 1;
            if (n < 64) {
                power <<= n;
                q = power / divisor;
                r = power % divisor;
                return 0;
            }
            // Divide numerand and divisor by 2^(n-63) to get approximation of result
            // Most of the way on divisor
            ulong y = divisor >> (n - 64);
            if (y == 0) {
                q = r = 0;
                // Check if result will be too big
                return 1;
            }
            // Divide divisor by final bit
            y >>= 1;
            power <<= 63;
            ulong max;
            if (y == 0) {
                max = 0;
                max -= 1;
                // Could be maximal
                // Check if divisor is a power of 2
                if ((1UL << (n - 64)) == divisor) {
                    q = r = 0;
                    return 1;
                }
            }
            else {
                max = power / y + 1;
            }
            ulong min = power / (y + 1);
            if (min != 0) {
                min -= 1;
            }
            ulong[] fullpower = { 0, ((ulong)1) << (n - 64) };
            ulong[] mult = { 0, 0} ;
            ulong tmpq = 0;
            while (max > min + 1) {
                tmpq = max + min;
                if (tmpq < min) {
                    tmpq = (tmpq >> 1) + 0x8000000000000000L;
                }
                else {
                    tmpq >>= 1;
                }
                mult64to128(mult, divisor, tmpq);
                if (unsignedCompare128(fullpower, mult) < 0) {
                    max = tmpq - 1;
                }
                else {
                    min = tmpq;
                }
            }
            // min is now our putative quotient
            if (tmpq != min) {
                mult64to128(mult, divisor, min);
            }
            // Calculate remainder
            unsignedSubtract128(fullpower, mult);
            // min might be 1 too small
            if (fullpower[1] != 0 || fullpower[0] >= divisor) {
                q = min + 1;
                r = fullpower[0] - divisor;
            }
            else {
                q = min;
                r = fullpower[0];
            }
            return 0;
        }

        internal static unsafe ulong Strtoul(char* buffer, out char* pNextCharacter,
            int @base)
        {
            if ((0 > @base) || (36 < @base)) {
                throw new ArgumentOutOfRangeException();
            }
            int index = 0;
            while('\0' != buffer[index]) {
                if (char.IsWhiteSpace(buffer[index])) {
                    index++;
                    continue;
                }
            }
            bool firstCharacter = true;
            while('\0' != buffer[index]) {
                if (firstCharacter) {
                    switch (buffer[index]) {
                        case '-':
                            throw new Exception("Negative number");
                        case '+':
                            firstCharacter = false;
                            index++;
                            continue;
                        default:
                            break;
                    }
                }
            }
            ulong result = 0;
            bool done = false;
            while ('\0' != buffer[index]) {
                char scannedCharacter = buffer[index];
                if (firstCharacter) {
                    switch (@base) {
                        case 0:
                            if ('0' == scannedCharacter) {
                                firstCharacter = false;
                                scannedCharacter = buffer[++index];
                                if (('x' == scannedCharacter)
                                    || ('X' == scannedCharacter))
                                {
                                    @base = 16;
                                    index++;
                                    continue;
                                }
                                @base = 8;
                                continue;
                            }
                            else {
                                @base = 10;
                            }
                            break;
                        case 8:
                            if ('0' == scannedCharacter) {
                                firstCharacter = false;
                                index++;
                                continue;
                            }
                            break;
                        case 16:
                            if ('0' == scannedCharacter) {
                                firstCharacter = false;
                                scannedCharacter = buffer[++index];
                                if (('x' == scannedCharacter)
                                    || ('X' == scannedCharacter))
                                {
                                    index++;
                                    continue;
                                }
                            }
                            break;
                    }
                }
                // Base is known and we can continue parsing.
                int characterValue = 0;
                if (('0' <= scannedCharacter) && ('9' >= scannedCharacter)) {
                    characterValue = scannedCharacter - '0';
                }
                else if (('a' <= scannedCharacter) && ('z' >= scannedCharacter)) {
                    characterValue = 10 + (scannedCharacter - 'a');
                }
                else if (('A' <= scannedCharacter) && ('Z' >= scannedCharacter)) {
                    characterValue = 10 + (scannedCharacter - 'A');
                }
                else {
                    done = true;
                }
                if (!done) {
                    if (characterValue >= @base) {
                        done = true;
                    }
                    else {
                        if ((ulong.MaxValue / (uint)@base) < result) {
                            throw new OverflowException();
                        }
                        result *= (uint)@base;
                        result += (uint)characterValue;
                    }
                }
                if (done) {
                    break;
                }
            }
            pNextCharacter = &(buffer[index]);
            return result;
        }

        public static unsafe int get_offset_size(char* ptr, ref ulong offset)
        {
            // Get optional size and offset fields from string
            int size;
            uint val;
            char* ptr2;

            // Defaults
            val = 0;
            size = -1;
            if (*ptr == ':') {
                size = (int)Strtoul(ptr + 1, out ptr2, 0);
                if (*ptr2 == '+') {
                    val = (uint)Strtoul(ptr2 + 1, out ptr2, 0);
                }
            }
            if (*ptr == '+') {
                val = (uint)Strtoul(ptr + 1, out ptr2, 0);
            }
            // Adjust offset
            offset += val;
            return size;
        }

        //extern bool Globals.signbit_negative(ulong val, int size); ///< Return true if the sign-bit is set
        //extern ulong Globals.calc_mask(int size);          ///< Calculate a mask for a given byte size
        //extern ulong Globals.uintb_negate(ulong in, int size);     ///< Negate the \e sized value
        //extern ulong Globals.sign_extend(ulong in, int sizein, int sizeout);  ///< Sign-extend a value between two byte sizes

        //extern void Globals.sign_extend(long &val, int bit);       ///< Sign extend above given bit
        //extern void Globals.zero_extend(long &val, int bit);       ///< Clear all bits above given bit
        //extern void Globals.byte_swap(long &val, int size);        ///< Swap bytes in the given value

        //extern ulong Globals.byte_swap(ulong val, int size);       ///< Return the given value with bytes swapped
        //extern int Globals.leastsigbit_set(ulong val);         ///< Return index of least significant bit set in given value
        //extern int Globals.mostsigbit_set(ulong val);          ///< Return index of most significant bit set in given value
        //extern int Globals.popcount(ulong val);            ///< Return the number of one bits in the given value
        //extern int Globals.count_leading_zeros(ulong val);     ///< Return the number of leading zero bits in the given value

        //extern ulong Globals.coveringmask(ulong val);           ///< Return a mask that \e covers the given value
        //extern int Globals.bit_transitions(ulong val, int sz);        ///< Calculate the number of bit transitions in the sized value

        //extern void Globals.mult64to128(ulong* res, ulong x, ulong y);
        //extern void Globals.unsignedSubtract128(ulong* a, ulong* b);
        //extern int Globals.unsignedCompare128(ulong* a, ulong* b);
        //extern int Globals.power2Divide(int n, ulong divisor, ulong &q, ulong &r);

        /// Return true if \b vn1 contains the high part and \b vn2 the low part
        /// of what was(is) a single value.
        /// \param vn1 is the putative high Varnode
        /// \param vn2 is the putative low Varnode
        /// \return \b true if they are pieces of a whole
        internal static bool contiguous_test(Varnode vn1, Varnode vn2)
        {
            if (vn1.isInput() || vn2.isInput()) {
                return false;
            }
            if ((!vn1.isWritten()) || (!vn2.isWritten())) return false;
            PcodeOp op1 = vn1.getDef() ?? throw new BugException();
            PcodeOp op2 = vn2.getDef();
            Varnode vnwhole;
            switch (op1.code()) {
                case OpCode.CPUI_SUBPIECE:
                    if (op2.code() != OpCode.CPUI_SUBPIECE) return false;
                    vnwhole = op1.getIn(0);
                    if (op2.getIn(0) != vnwhole) return false;
                    if (op2.getIn(1).getOffset() != 0)
                        return false;       // Must be least sig
                    if (op1.getIn(1).getOffset() != (uint)vn2.getSize())
                        return false;       // Must be contiguous
                    return true;
                default:
                    return false;
            }
        }

        /// Assuming vn1,vn2 has passed the Globals.contiguous_test(), return
        /// the Varnode containing the whole value.
        /// \param data is the underlying function
        /// \param vn1 is the high Varnode
        /// \param vn2 is the low Varnode
        /// \return the whole Varnode
        internal static Varnode? findContiguousWhole(Funcdata data, Varnode vn1, Varnode vn2)
        {
            if (vn1.isWritten())
                if (vn1.getDef().code() == OpCode.CPUI_SUBPIECE)
                    return vn1.getDef().getIn(0);
            return (Varnode)null;
        }

        internal static int run_xml(string filein, SleighCompile compiler)
        {
            StreamReader s = new StreamReader(File.OpenRead(filein));
            Document doc;
            string specfileout = string.Empty;
            string specfilein = string.Empty;

            try {
                doc = Xml.xml_tree(s);
            }
            catch (DecoderError) {
                Console.Error.WriteLine($"Unable to parse single input file as XML spec: {filein}");
                return 1;
            }
            s.Close();

            Element el = doc.getRoot() ?? throw new BugException();
            while(true) {
                List<Element> list = el.getChildren();
                bool listCompleted = false;
                IEnumerator<Element> iter = list.GetEnumerator();
                while (true) {
                    if (!iter.MoveNext()) {
                        listCompleted = true;
                        break;
                    }
                    el = iter.Current;
                    if (el.getName() == "processorfile") {
                        specfileout = el.getContent();
                        int num = el.getNumAttributes();
                        for (int i = 0; i < num; ++i) {
                            if (el.getAttributeName(i) == "slaspec")
                                specfilein = el.getAttributeValue(i);
                            else {
                                compiler.setPreprocValue(el.getAttributeName(i), el.getAttributeValue(i));
                            }
                        }
                    }
                    else if (el.getName() == "language_spec")
                        break;
                    else if (el.getName() == "language_description")
                        break;
                }
                if (listCompleted) break;
            }
            // delete doc;

            if (specfilein.Length == 0) {
                Console.Error.WriteLine($"Input slaspec file was not specified in {filein}");
                return 1;
            }
            if (specfileout.Length == 0) {
                Console.Error.WriteLine($"Output sla file was not specified in {filein}");
                return 1;
            }
            return compiler.run_compilation(specfilein, specfileout);
        }

        internal static void findSlaSpecs(List<string> res, string dir, string suffix)
        {
            FileManage.matchListDir(res, suffix, true, dir, false);

            List<string> dirs = new List<string>();
            FileManage.directoryList(dirs, dir);
            foreach (string nextdir in dirs) {
                Globals.findSlaSpecs(res, nextdir, suffix);
            }
        }
        internal static void segvHandler(int sig)
        {
            exit(1);            // Just die - prevents OS from popping-up a dialog
        }

        internal static int pcodelex()
        {
            return pcode.lex();
        }

        internal static int pcodeerror(string s)
        {
            pcode.reportError((Location)null, s);
            return 0;
        }

        // ------------------ OpCode related ------------------------------
        /// \brief Names of operations associated with their opcode number
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
        /// \return the complementary OpCode or OpCode.CPUI_MAX if not given a comparison operation
        public static OpCode get_booleanflip(OpCode opc, out bool reorder)
        {
            reorder = false;
            switch (opc) {
                case OpCode.CPUI_INT_EQUAL:
                    return OpCode.CPUI_INT_NOTEQUAL;
                case OpCode.CPUI_INT_NOTEQUAL:
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
                    return OpCode.CPUI_COPY;
                case OpCode.CPUI_FLOAT_EQUAL:
                    return OpCode.CPUI_FLOAT_NOTEQUAL;
                case OpCode.CPUI_FLOAT_NOTEQUAL:
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

        // ------------------------ Crc32 related -------------------------------
        /// Table for quickly computing a 32-bit Cyclic Redundacy Check (CRC)
        private static uint[] crc32tab = {
            0x0,0x77073096,0xee0e612c,0x990951ba,0x76dc419,0x706af48f,
            0xe963a535,0x9e6495a3,0xedb8832,0x79dcb8a4,0xe0d5e91e,
            0x97d2d988,0x9b64c2b,0x7eb17cbd,0xe7b82d07,0x90bf1d91,
            0x1db71064,0x6ab020f2,0xf3b97148,0x84be41de,0x1adad47d,
            0x6ddde4eb,0xf4d4b551,0x83d385c7,0x136c9856,0x646ba8c0,
            0xfd62f97a,0x8a65c9ec,0x14015c4f,0x63066cd9,0xfa0f3d63,
            0x8d080df5,0x3b6e20c8,0x4c69105e,0xd56041e4,0xa2677172,
            0x3c03e4d1,0x4b04d447,0xd20d85fd,0xa50ab56b,0x35b5a8fa,
            0x42b2986c,0xdbbbc9d6,0xacbcf940,0x32d86ce3,0x45df5c75,
            0xdcd60dcf,0xabd13d59,0x26d930ac,0x51de003a,0xc8d75180,
            0xbfd06116,0x21b4f4b5,0x56b3c423,0xcfba9599,0xb8bda50f,
            0x2802b89e,0x5f058808,0xc60cd9b2,0xb10be924,0x2f6f7c87,
            0x58684c11,0xc1611dab,0xb6662d3d,0x76dc4190,0x1db7106,
            0x98d220bc,0xefd5102a,0x71b18589,0x6b6b51f,0x9fbfe4a5,
            0xe8b8d433,0x7807c9a2,0xf00f934,0x9609a88e,0xe10e9818,
            0x7f6a0dbb,0x86d3d2d,0x91646c97,0xe6635c01,0x6b6b51f4,
            0x1c6c6162,0x856530d8,0xf262004e,0x6c0695ed,0x1b01a57b,
            0x8208f4c1,0xf50fc457,0x65b0d9c6,0x12b7e950,0x8bbeb8ea,
            0xfcb9887c,0x62dd1ddf,0x15da2d49,0x8cd37cf3,0xfbd44c65,
            0x4db26158,0x3ab551ce,0xa3bc0074,0xd4bb30e2,0x4adfa541,
            0x3dd895d7,0xa4d1c46d,0xd3d6f4fb,0x4369e96a,0x346ed9fc,
            0xad678846,0xda60b8d0,0x44042d73,0x33031de5,0xaa0a4c5f,
            0xdd0d7cc9,0x5005713c,0x270241aa,0xbe0b1010,0xc90c2086,
            0x5768b525,0x206f85b3,0xb966d409,0xce61e49f,0x5edef90e,
            0x29d9c998,0xb0d09822,0xc7d7a8b4,0x59b33d17,0x2eb40d81,
            0xb7bd5c3b,0xc0ba6cad,0xedb88320,0x9abfb3b6,0x3b6e20c,
            0x74b1d29a,0xead54739,0x9dd277af,0x4db2615,0x73dc1683,
            0xe3630b12,0x94643b84,0xd6d6a3e,0x7a6a5aa8,0xe40ecf0b,
            0x9309ff9d,0xa00ae27,0x7d079eb1,0xf00f9344,0x8708a3d2,
            0x1e01f268,0x6906c2fe,0xf762575d,0x806567cb,0x196c3671,
            0x6e6b06e7,0xfed41b76,0x89d32be0,0x10da7a5a,0x67dd4acc,
            0xf9b9df6f,0x8ebeeff9,0x17b7be43,0x60b08ed5,0xd6d6a3e8,
            0xa1d1937e,0x38d8c2c4,0x4fdff252,0xd1bb67f1,0xa6bc5767,
            0x3fb506dd,0x48b2364b,0xd80d2bda,0xaf0a1b4c,0x36034af6,
            0x41047a60,0xdf60efc3,0xa867df55,0x316e8eef,0x4669be79,
            0xcb61b38c,0xbc66831a,0x256fd2a0,0x5268e236,0xcc0c7795,
            0xbb0b4703,0x220216b9,0x5505262f,0xc5ba3bbe,0xb2bd0b28,
            0x2bb45a92,0x5cb36a04,0xc2d7ffa7,0xb5d0cf31,0x2cd99e8b,
            0x5bdeae1d,0x9b64c2b0,0xec63f226,0x756aa39c,0x26d930a,
            0x9c0906a9,0xeb0e363f,0x72076785,0x5005713,0x95bf4a82,
            0xe2b87a14,0x7bb12bae,0xcb61b38,0x92d28e9b,0xe5d5be0d,
            0x7cdcefb7,0xbdbdf21,0x86d3d2d4,0xf1d4e242,0x68ddb3f8,
            0x1fda836e,0x81be16cd,0xf6b9265b,0x6fb077e1,0x18b74777,
            0x88085ae6,0xff0f6a70,0x66063bca,0x11010b5c,0x8f659eff,
            0xf862ae69,0x616bffd3,0x166ccf45,0xa00ae278,0xd70dd2ee,
            0x4e048354,0x3903b3c2,0xa7672661,0xd06016f7,0x4969474d,
            0x3e6e77db,0xaed16a4a,0xd9d65adc,0x40df0b66,0x37d83bf0,
            0xa9bcae53,0xdebb9ec5,0x47b2cf7f,0x30b5ffe9,0xbdbdf21c,
            0xcabac28a,0x53b39330,0x24b4a3a6,0xbad03605,0xcdd70693,
            0x54de5729,0x23d967bf,0xb3667a2e,0xc4614ab8,0x5d681b02,
            0x2a6f2b94,0xb40bbe37,0xc30c8ea1,0x5a05df1b,0x2d02ef8d };

        /// \brief Feed 8 bits into a CRC register
        ///
        /// \param reg is the current state of the CRC register
        /// \param val holds 8 bits (least significant) to feed in
        /// \return the new value of the register
        internal static uint crc_update(uint reg, uint val)
        {
            return crc32tab[(reg ^ val) & 0xff] ^ (reg >> 8);
        }
    }
}
