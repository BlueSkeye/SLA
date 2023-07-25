using Sla.CORE;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.ConstrainedExecution;
using System.Text;
using System.Threading.Tasks;
using static System.Net.WebRequestMethods;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace ghidra
{
    public static partial class Globals
    {
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

        /// Perform a CPUI_INT_RIGHT on the given val
        /// \param val is the value to shift
        /// \param sa is the number of bits to shift
        /// \return the shifted value
        public static ulong pcode_right(ulong val, int sa)
        {
            if (sa >= 8 * sizeof(ulong))
            {
                return 0;
            }
            return val >> sa;
        }

        /// Perform a CPUI_INT_LEFT on the given val
        /// \param val is the value to shift
        /// \param sa is the number of bits to shift
        /// \return the shifted value
        public static ulong pcode_left(ulong val, int sa)
        {
            if (sa >= 8 * sizeof(ulong))
            {
                return 0;
            }
            return val << sa;
        }

        /// \brief Calculate smallest mask that covers the given value
        /// Calculcate a mask that covers either the least significant byte, uint2, uint, or ulong,
        /// whatever is smallest.
        /// \param val is the given value
        /// \return the minimal mask
        public static ulong minimalmask(ulong val)
        {
            if (val > 0xffffffff)
            {
                return ~((ulong)0);
            }
            if (val > 0xffff)
            {
                return 0xffffffff;
            }
            if (val > 0xff)
            {
                return 0xffff;
            }
            return 0xff;
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
            return ((~@in) & calc_mask((uint)size));
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
        public static ulong byte_swap(ulong val, int size)
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
            ulong mask = ~((ulong)0);
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
            ulong mask = ~((ulong)0);
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
            ulong mask = ~((ulong)0);
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
        /// Scanning across the bits of \b val return the number of transitions (from 0->1 or 1->0)
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
        public static int power2Divide(int n, ulong divisor, ref ulong q, ref ulong r)
        {
            if (divisor == 0) {
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
                if ((((ulong)1) << (n - 64)) == divisor) {
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

        //extern bool signbit_negative(ulong val, int size); ///< Return true if the sign-bit is set
        //extern ulong calc_mask(int size);          ///< Calculate a mask for a given byte size
        //extern ulong uintb_negate(ulong in, int size);     ///< Negate the \e sized value
        //extern ulong sign_extend(ulong in, int sizein, int sizeout);  ///< Sign-extend a value between two byte sizes

        //extern void sign_extend(long &val, int bit);       ///< Sign extend above given bit
        //extern void zero_extend(long &val, int bit);       ///< Clear all bits above given bit
        //extern void byte_swap(long &val, int size);        ///< Swap bytes in the given value

        //extern ulong byte_swap(ulong val, int size);       ///< Return the given value with bytes swapped
        //extern int leastsigbit_set(ulong val);         ///< Return index of least significant bit set in given value
        //extern int mostsigbit_set(ulong val);          ///< Return index of most significant bit set in given value
        //extern int popcount(ulong val);            ///< Return the number of one bits in the given value
        //extern int count_leading_zeros(ulong val);     ///< Return the number of leading zero bits in the given value

        //extern ulong coveringmask(ulong val);           ///< Return a mask that \e covers the given value
        //extern int bit_transitions(ulong val, int sz);        ///< Calculate the number of bit transitions in the sized value

        //extern void mult64to128(ulong* res, ulong x, ulong y);
        //extern void unsignedSubtract128(ulong* a, ulong* b);
        //extern int unsignedCompare128(ulong* a, ulong* b);
        //extern int power2Divide(int n, ulong divisor, ulong &q, ulong &r);

        /// Return true if \b vn1 contains the high part and \b vn2 the low part
        /// of what was(is) a single value.
        /// \param vn1 is the putative high Varnode
        /// \param vn2 is the putative low Varnode
        /// \return \b true if they are pieces of a whole
        internal static bool contiguous_test(Varnode vn1, Varnode vn2)
        {
            if (vn1->isInput() || vn2->isInput())
            {
                return false;
            }
            if ((!vn1->isWritten()) || (!vn2->isWritten())) return false;
            PcodeOp* op1 = vn1->getDef();
            PcodeOp* op2 = vn2->getDef();
            Varnode* vnwhole;
            switch (op1->code())
            {
                case CPUI_SUBPIECE:
                    if (op2->code() != CPUI_SUBPIECE) return false;
                    vnwhole = op1->getIn(0);
                    if (op2->getIn(0) != vnwhole) return false;
                    if (op2->getIn(1)->getOffset() != 0)
                        return false;       // Must be least sig
                    if (op1->getIn(1)->getOffset() != vn2->getSize())
                        return false;       // Must be contiguous
                    return true;
                default:
                    return false;
            }
        }

        /// Assuming vn1,vn2 has passed the contiguous_test(), return
        /// the Varnode containing the whole value.
        /// \param data is the underlying function
        /// \param vn1 is the high Varnode
        /// \param vn2 is the low Varnode
        /// \return the whole Varnode
        internal static Varnode findContiguousWhole(Funcdata data, Varnode vn1, Varnode vn2)
        {
            if (vn1->isWritten())
                if (vn1->getDef()->code() == CPUI_SUBPIECE)
                    return vn1->getDef()->getIn(0);
            return (Varnode*)0;
        }

        internal static int4 run_xml(string filein, SleighCompile compiler)
        {
            ifstream s = new ifstream(filein);
            Document doc;
            string specfileout;
            string specfilein;

            try
            {
                doc = xml_tree(s);
            }
            catch (DecoderError)
            {
                cerr << "Unable to parse single input file as XML spec: " << filein << endl;
                exit(1);
            }
            s.close();

            Element* el = doc->getRoot();
            for (; ; )
            {
                List & list(el->getChildren());
                List::const_iterator iter;
                for (iter = list.begin(); iter != list.end(); ++iter)
                {
                    el = *iter;
                    if (el->getName() == "processorfile")
                    {
                        specfileout = el->getContent();
                        int4 num = el->getNumAttributes();
                        for (int4 i = 0; i < num; ++i)
                        {
                            if (el->getAttributeName(i) == "slaspec")
                                specfilein = el->getAttributeValue(i);
                            else
                            {
                                compiler.setPreprocValue(el->getAttributeName(i), el->getAttributeValue(i));
                            }
                        }
                    }
                    else if (el->getName() == "language_spec")
                        break;
                    else if (el->getName() == "language_description")
                        break;
                }
                if (iter == list.end()) break;
            }
            delete doc;

            if (specfilein.size() == 0)
            {
                cerr << "Input slaspec file was not specified in " << filein << endl;
                exit(1);
            }
            if (specfileout.size() == 0)
            {
                cerr << "Output sla file was not specified in " << filein << endl;
                exit(1);
            }
            return compiler.run_compilation(specfilein, specfileout);
        }

        internal static void findSlaSpecs(vector<string> res, string dir, string suffix)
        {
            FileManage::matchListDir(res, suffix, true, dir, false);

            vector<string> dirs;
            FileManage::directoryList(dirs, dir);
            vector<string>::const_iterator iter;
            for (iter = dirs.begin(); iter != dirs.end(); ++iter)
            {
                const string &nextdir(*iter);
                findSlaSpecs(res, nextdir, suffix);
            }
        }
        internal static void segvHandler(int sig)
        {
            exit(1);            // Just die - prevents OS from popping-up a dialog
        }
    }
}
