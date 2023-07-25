/* ###
 * IP: GHIDRA
 * NOTE: uses some windows and sparc specific floating point definitions
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
//#include "float.hh"
//#include "address.hh"

//#include <cmath>
//#include <limits>

using System;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading;
using System.Xml.Linq;

namespace Sla.CORE
{
    //using std::ldexp;
    //using std::frexp;
    //using std::signbit;
    //using std::sqrt;
    //using std::floor;
    //using std::ceil;
    //using std::round;
    //using std::fabs;

    /// \brief Encoding information for a single floating-point format
    /// This class supports manipulation of a single floating-point encoding.
    /// An encoding can be converted to and from the host format and
    /// convenience methods allow p-code floating-point operations to be
    /// performed on natively encoded operands.  This follows the IEEE754 standards.
    public class FloatFormat
    {
        /// \brief The various classes of floating-point encodings
        public enum floatclass
        {
            normalized = 0,		///< A normal floating-point number
            infinity = 1,		///< An encoding representing an infinite value
            zero = 2,			///< An encoding of the value zero
            nan = 3,			///< An invalid encoding, Not-a-Number
            denormalized = 4		///< A denormalized encoding (for very small values)
        };

        private int size;			///< Size of float in bytes (this format)
        private int signbit_pos;		///< Bit position of sign bit
        private int frac_pos;		///< (lowest) bit position of fractional part
        private int frac_size;		///< Number of bits in fractional part
        private int exp_pos;			///< (lowest) bit position of exponent
        private int exp_size;		///< Number of bits in exponent
        private int bias;			///< What to add to real exponent to get encoding
        private int maxexponent;		///< Maximum possible exponent
        private int decimal_precision;	///< Number of decimal digits of precision
        private bool jbitimplied;		///< Set to \b true if integer bit of 1 is assumed

        ///< Create a double given sign, fractional, and exponent
        /// \param sign is set to \b true if the value should be negative
        /// \param signif is the fractional part
        /// \param exp is the exponent
        /// \return the constructed floating-point value
        private static double createFloat(bool sign, ulong signif, int exp)
        {
            signif >>= 1;             // Throw away 1 bit of precision we will
                                      // lose anyway, to make sure highbit is 0
            int precis = 8 * sizeof(ulong) - 1;   // fullword - 1 we threw away
            double res = (double)signif;
            int expchange = exp - precis + 1; // change in exponent is precis
                                               // -1 integer bit
            res = res * double.Log2(expchange);
            if (sign) {
                res = res * -1.0;
            }
            return res;
        }

        /// \brief Extract the sign, fractional, and exponent from a given floating-point value
        /// \param x is the given value
        /// \param sgn passes back the sign
        /// \param signif passes back the fractional part
        /// \param exp passes back the exponent
        /// \return the floating-point class of the value
        private static floatclass extractExpSig(double x, out bool sgn, out ulong signif,
            out int exp)
        {
            int e;

            sgn = double.IsNegative(x);
            signif = 0;
            exp = 0;
            if (x == 0.0) {
                return floatclass.zero;
            }
            if (double.IsInfinity(x)) {
                return floatclass.infinity;
            }
            if (double.IsNaN(x)) {
                return floatclass.nan;
            }
            if (sgn) {
                x = -x;
            }
            // norm is between 1/2 and 1
            double norm = Decompose(x, out e);
            norm *= double.Exp2(8 * sizeof(ulong) - 1); // norm between 2^62 and 2^63

            // Convert to normalized integer
            signif = (ulong)norm;
            signif <<= 1;

            // Consider normalization between 1 and 2  
            e -= 1;
            exp = e;
            return floatclass.normalized;
        }

        // Implementation of the frexp C++ stdlib function.
        private static double Decompose(double value, out int exponent)
        {
            exponent = 0;
            if (1.0 > value) {
                while (0.5 > value) {
                    exponent--;
                    value *= 2.0;
                }
            }
            else {
                while(1.0 < value) {
                    exponent++;
                    value /= 2.0;
                }
            }
            return value;
        }

        /// \brief Round a floating point value to the nearest even
        /// \param signif the significant bits of a floating point value
        /// \param lowbitpos the position in signif of the floating point
        /// \return true if we rounded up
        private static bool roundToNearestEven(ulong signif, int lowbitpos)
        {
            ulong lowbitmask = (lowbitpos < 8 * sizeof(ulong)) ? ((ulong)1 << lowbitpos) : 0;
            ulong midbitmask = (ulong)1 << (lowbitpos - 1);
            ulong epsmask = midbitmask - 1;
            bool odd = (signif & lowbitmask) != 0;
            if ((signif & midbitmask) != 0 && ((signif & epsmask) != 0 || odd)) {
                signif += midbitmask;
                return true;
            }
            return false;
        }

        /// Set the fractional part of an encoded value
        /// \param x is an encoded value (with fraction part set to zero)
        /// \param code is the new fractional value to set
        /// \return the encoded value with the fractional filled in
        private ulong setFractionalCode(ulong x, ulong code)
        {
            // Align with bottom of word, also drops bits of precision
            // we don't have room for
            code >>= 8*sizeof(ulong) - frac_size;
            code <<= frac_pos;		// Move bits into position;
            return x | code;
        }

        ///< Set the sign bit of an encoded value
        /// \param x is an encoded value (with sign set to zero)
        /// \param sign is the sign bit to set
        /// \return the encoded value with the sign bit set
        private ulong setSign(ulong x, bool sign)
        {
            if (!sign) {
                // Assume bit is already zero
                return x;
            }
            ulong mask = 1;
            mask <<= signbit_pos;
            // Stick in the bit
            x |= mask;
            return x;
        }

        ///< Set the exponent of an encoded value
        /// \param x is an encoded value (with exponent set to zero)
        /// \param code is the exponent to set
        /// \return the encoded value with the new exponent
        private ulong setExponentCode(ulong x,ulong code)

        {
            code <<= exp_pos;		// Move bits into position
            x |= code;
            return x;
        }

        ///< Get an encoded zero value
        /// \param sgn is set to \b true for negative zero, \b false for positive
        /// \return the encoded zero
        private ulong getZeroEncoding(bool sgn)
        {
            ulong res = 0;
            // Use IEEE 754 standard for zero encoding
            res = setFractionalCode(res,0);
            res = setExponentCode(res,0);
            return setSign(res,sgn);
        }

        ///< Get an encoded infinite value
        /// \param sgn is set to \b true for negative infinity, \b false for positive
        /// \return the encoded infinity
        private ulong getInfinityEncoding(bool sgn)
        {
            ulong res = 0;
            // Use IEEE 754 standard for infinity encoding
            res = setFractionalCode(res,0);
            res = setExponentCode(res,(ulong)maxexponent);
            return setSign(res,sgn);
        }

        ///< Get an encoded NaN value
        /// \param sgn is set to \b true for negative NaN, \b false for positive
        /// \return the encoded NaN
        private ulong getNaNEncoding(bool sgn)
        {
            ulong res = 0;
            // Use IEEE 754 standard for NaN encoding
            ulong mask = 1;
            mask <<= 8*sizeof(ulong)-1;	// Create "quiet" NaN
            res = setFractionalCode(res,mask);
            res = setExponentCode(res,(ulong)maxexponent);
            return setSign(res,sgn);
        }

        ///< Calculate the decimal precision of this format
        private void calcPrecision()
        {
            float val = (float)(frac_size * 0.30103);
            decimal_precision = (int)double.Floor(val + 0.5);
        }

        ///< Construct for use with restoreXml()
        public FloatFormat()
        {
        }

        ///< Construct default IEEE 754 standard settings
        /// Set format for a given encoding size according to IEEE 754 standards
        /// \param sz is the size of the encoding in bytes
        public FloatFormat(int sz)
        {
            size = sz;

            if (size == 4) {
                signbit_pos = 31;
                exp_pos = 23;
                exp_size = 8;
                frac_pos = 0;
                frac_size = 23;
                bias = 127;
                jbitimplied = true;
            }
            else if (size == 8) {
                signbit_pos = 63;
                exp_pos = 52;
                exp_size = 11;
                frac_pos = 0;
                frac_size = 52;
                bias = 1023;
                jbitimplied = true;
            }
            maxexponent = (1 << exp_size) - 1;
            calcPrecision();
        }

        ///< Get the size of the encoding in bytes
        public int getSize()
        {
            return size;
        }

        ///< Convert an encoding into host's double
        /// \param encoding is the encoding value
        /// \param type points to the floating-point class, which is passed back
        /// \return the equivalent double value
        public double getHostFloat(ulong encoding, out floatclass type)
        {
            bool sgn = extractSign(encoding);
            ulong frac = extractFractionalCode(encoding);
            int exp = extractExponentCode(encoding);
            bool normal = true;

            if (exp == 0) {
                if (frac == 0 ) {		// Floating point zero
                    type = floatclass.zero;
                    return sgn? -0.0 : +0.0;
                }
                type = floatclass.denormalized;
                // Number is denormalized
                normal = false;
            }
            else if (exp == maxexponent) {
                if (frac == 0) {
                    // Floating point infinity
                    type = floatclass.infinity;
                    return sgn ? double.NegativeInfinity : double.PositiveInfinity;
                }
                type = floatclass.nan;
                // encoding is "Not a Number" NaN
                return sgn ? -double.NaN : +double.NaN; // Sign is usually ignored
            }
            else {
                type = floatclass.normalized;
            }

            // Get "true" exponent and fractional
            exp -= bias;
            if (normal && jbitimplied) {
                frac >>= 1;         // Make room for 1 jbit
                ulong highbit = 1;
                highbit <<= 8 * sizeof(ulong) - 1;
                frac |= highbit;        // Stick bit in at top
            }
            return createFloat(sgn, frac, exp);
        }

        ///< Convert host's double into \b this encoding
        /// \param host is the double value to convert
        /// \return the equivalent encoded value
        public ulong getEncoding(double host)
        {
            floatclass type;
            bool sgn;
            ulong signif;
            int exp;

            type = extractExpSig(host, out sgn, out signif, out exp);
            if (type == floatclass.zero) {
                return getZeroEncoding(sgn);
            }
            else if (type == floatclass.infinity) {
                return getInfinityEncoding(sgn);
            }
            else if (type == floatclass.nan) {
                return getNaNEncoding(sgn);
            }

            // convert exponent and fractional to their encodings
            exp += bias;
            if (exp < -frac_size) {
                // Exponent is too small to represent
                return getZeroEncoding(sgn); // TODO handle round to non-zero
            }

            if (exp < 1) {
                // Must be denormalized
                if (roundToNearestEven(signif, 8 * sizeof(ulong) - frac_size - exp)) {
                    // TODO handle round to normal case
                    if ((signif >> (8 * sizeof(ulong) - 1)) == 0) {
                        signif = (ulong)1 << (8 * sizeof(ulong) - 1);
                        exp += 1;
                    }
                }
                return setFractionalCode(getZeroEncoding(sgn), signif >> (-exp));
            }
            if (roundToNearestEven(signif, 8 * sizeof(ulong) - frac_size - 1)) {
                // if high bit is clear, then the add overflowed. Increase exp and set
                // signif to 1.
                if ((signif >> (8 * sizeof(ulong) - 1)) == 0) {
                    signif = (ulong)1 << (8 * sizeof(ulong) - 1);
                    exp += 1;
                }
            }

            if (exp >= maxexponent) {
                // Exponent is too big to represent
                return getInfinityEncoding(sgn);
            }
            if (jbitimplied && (exp != 0)) {
                signif <<= 1;		// Cut off top bit (which should be 1)
            }
            ulong res = 0;
            res = setFractionalCode(res, signif);
            res = setExponentCode(res, (ulong)exp);
            return setSign(res, sgn);
        }
        
        ///< Get number of digits of precision
        public int getDecimalPrecision()
        {
            return decimal_precision;
        }

        ///< Convert between two different formats
        /// \param encoding is the value in the \e other FloatFormat
        /// \param formin is the \e other FloatFormat
        /// \return the equivalent value in \b this FloatFormat
        public ulong convertEncoding(ulong encoding, FloatFormat formin)
        {
            bool sgn = formin.extractSign(encoding);
            ulong signif = formin.extractFractionalCode(encoding);
            int exp = formin.extractExponentCode(encoding);

            if (exp == formin.maxexponent) {
                // NaN or INFINITY encoding
                exp = maxexponent;
                if (signif != 0) {
                    return getNaNEncoding(sgn);
                }
                else {
                    return getInfinityEncoding(sgn);
                }
            }

            if (exp == 0) { // incoming is subnormal
                if (signif == 0) {
                    return getZeroEncoding(sgn);
                }

                // normalize
                int lz = Globals.count_leading_zeros(signif);
                signif <<= lz;
                exp = -formin.bias - lz;
            }
            else {
                // incoming is normal
                exp -= formin.bias;
                if (jbitimplied) {
                    signif = ((ulong)1 << (8 * sizeof(ulong) - 1)) | (signif >> 1);
                }
            }
            exp += bias;
            if (exp < -frac_size) {
                // Exponent is too small to represent
                return getZeroEncoding(sgn); // TODO handle round to non-zero
            }

            if (exp < 1) {
                // Must be denormalized
                if (roundToNearestEven(signif, 8 * sizeof(ulong) - frac_size - exp)) {
                    // TODO handle carry to normal case
                    if ((signif >> (8 * sizeof(ulong) - 1)) == 0) {
                        signif = (ulong)1 << (8 * sizeof(ulong) - 1);
                        exp += 1;
                    }
                }
                return setFractionalCode(getZeroEncoding(sgn), signif >> (-exp));
            }
            if (roundToNearestEven(signif, 8 * sizeof(ulong) - frac_size - 1)) {
                // if high bit is clear, then the add overflowed. Increase exp and set
                // signif to 1.
                if ((signif >> (8 * sizeof(ulong) - 1)) == 0) {
                    signif = (ulong)1 << (8 * sizeof(ulong) - 1);
                    exp += 1;
                }
            }

            if (exp >= maxexponent) {
                // Exponent is too big to represent
                return getInfinityEncoding(sgn);
            }
            if (jbitimplied && (exp != 0)) {
                // Cut off top bit (which should be 1)
                signif <<= 1;
            }
            ulong res = 0;
            res = setFractionalCode(res, signif);
            res = setExponentCode(res, (ulong)exp);
            return setSign(res, sgn);
        }

        ///< Extract the fractional part of the encoding
        /// \param x is an encoded floating-point value
        /// \return the fraction part of the value aligned to the top of the word
        public ulong extractFractionalCode(ulong x)
        {
            x >>= frac_pos;		// Eliminate bits below
            x <<= 8*sizeof(ulong) - frac_size; // Align with top of word
            return x;
        }

        ///< Extract the sign bit from the encoding
        /// \param x is an encoded floating-point value
        /// \return the sign bit
        public bool extractSign(ulong x)
        {
            x >>= signbit_pos;
            return ((x&1)!=0);
        }

        ///< Extract the exponent from the encoding
        /// \param x is an encoded floating-point value
        /// \return the (signed) exponent
        public int extractExponentCode(ulong x)
        {
            x >>= exp_pos;
            ulong mask = 1;
            mask = (mask<<exp_size) - 1;
            return (int) (x & mask);
        }

        // Operations on floating point values
        ///< Equality comparison (==)
        // Currently we emulate floating point operations on the target
        // By converting the encoding to the host's encoding and then
        // performing the operation using the host's floating point unit
        // then the host's encoding is converted back to the targets encoding
        /// \param a is the first floating-point value
        /// \param b is the second floating-point value
        /// \return \b true if (a == b)
        public ulong opEqual(ulong a, ulong b)
        {
            floatclass type;
            double val1 = getHostFloat(a, out type);
            double val2 = getHostFloat(b, out type);
            ulong res = (val1 == val2) ? 1UL : 0;
            return res;
        }

        ///< Inequality comparison (!=)
        /// \param a is the first floating-point value
        /// \param b is the second floating-point value
        /// \return \b true if (a != b)
        public ulong opNotEqual(ulong a, ulong b)
        {
            floatclass type;
            double val1 = getHostFloat(a, out type);
            double val2 = getHostFloat(b, out type);
            ulong res = (val1 != val2) ? 1UL : 0;
            return res;
        }

        ///< Less-than comparison (<)
        /// \param a is the first floating-point value
        /// \param b is the second floating-point value
        /// \return \b true if (a < b)
        public ulong opLess(ulong a, ulong b)
        {
            floatclass type;
            double val1 = getHostFloat(a, out type);
            double val2 = getHostFloat(b, out type);
            ulong res = (val1 < val2) ? 1UL : 0;
            return res;
        }

        ///< Less-than-or-equal comparison (<=)
        /// \param a is the first floating-point value
        /// \param b is the second floating-point value
        /// \return \b true if (a <= b)
        public ulong opLessEqual(ulong a, ulong b)
        {
            floatclass type;
            double val1 = getHostFloat(a, out type);
            double val2 = getHostFloat(b, out type);
            ulong res = (val1 <= val2) ? 1UL : 0;
            return res;
        }

        ///< Test if Not-a-Number (NaN)
        /// \param a is an encoded floating-point value
        /// \return \b true if a is Not-a-Number
        public ulong opNan(ulong a)
        {
            floatclass type;
            getHostFloat(a,out type);
            ulong res = (type == FloatFormat.floatclass.nan) ? 1UL : 0;
            return res;
        }

        ///< Addition (+)
        /// \param a is the first floating-point value
        /// \param b is the second floating-point value
        /// \return a + b
        public ulong opAdd(ulong a, ulong b)
        {
            floatclass type;
            double val1 = getHostFloat(a, out type);
            double val2 = getHostFloat(b, out type);
            return getEncoding(val1 + val2);
        }

        ///< Division (/)
        /// \param a is the first floating-point value
        /// \param b is the second floating-point value
        /// \return a / b
        public ulong opDiv(ulong a, ulong b)
        {
            floatclass type;
            double val1 = getHostFloat(a, out type);
            double val2 = getHostFloat(b, out type);
            return getEncoding(val1 / val2);
        }

        ///< Multiplication (*)
        /// \param a is the first floating-point value
        /// \param b is the second floating-point value
        /// \return a * b
        public ulong opMult(ulong a, ulong b)
        {
            floatclass type;
            double val1 = getHostFloat(a, out type);
            double val2 = getHostFloat(b, out type);
            return getEncoding(val1* val2);
        }

        ///< Subtraction (-)
        /// \param a is the first floating-point value
        /// \param b is the second floating-point value
        /// \return a - b
        public ulong opSub(ulong a, ulong b)
        {
            floatclass type;
            double val1 = getHostFloat(a, out type);
            double val2 = getHostFloat(b, out type);
            return getEncoding(val1 - val2);
        }

        ///< Unary negate
        /// \param a is an encoded floating-point value
        /// \return -a
        public ulong opNeg(ulong a)
        {
            floatclass type;
            double val = getHostFloat(a, out type);
            return getEncoding(-val);
        }

        ///< Absolute value (abs)
        /// \param a is an encoded floating-point value
        /// \return abs(a)
        public ulong opAbs(ulong a)
        {
            floatclass type;
            double val = getHostFloat(a, out type);
            return getEncoding(Double.Abs(val));
        }

        ///< Square root (sqrt)
        /// \param a is an encoded floating-point value
        /// \return sqrt(a)
        public ulong opSqrt(ulong a)
        {
            floatclass type;
            double val = getHostFloat(a, out type);
            return getEncoding(Double.Sqrt(val));
        }

        ///< Convert floating-point to integer
        /// \param a is an encoded floating-point value
        /// \param sizeout is the desired encoding size of the output
        /// \return an integer encoding of a
        public ulong opTrunc(ulong a, int sizeout)
        {
            floatclass type;
            double val = getHostFloat(a, out type);
            long ival = (long)val;  // Convert to integer
            ulong res = (ulong)ival;    // Convert to unsigned
            res &= Globals.calc_mask((uint)sizeout);	// Truncate to proper size
            return res;
        }

        ///< Ceiling (ceil)
        /// \param a is an encoded floating-point value
        /// \return ceil(a)
        public ulong opCeil(ulong a)
        {
            floatclass type;
            double val = getHostFloat(a, out type);
            return getEncoding(Double.Ceiling(val));
        }

        ///< Floor (floor)
        /// \param a is an encoded floating-point value
        /// \return floor(a)
        public ulong opFloor(ulong a)
        {
            floatclass type;
            double val = getHostFloat(a, out type);
            return getEncoding(Double.Floor(val));
        }

        ///< Round
        /// \param a is an encoded floating-point value
        /// \return round(a)
        public ulong opRound(ulong a)
        {
            floatclass type;
            double val = getHostFloat(a, out type);
            // return getEncoding(floor(val+.5)); // round half up
            return getEncoding(Double.Round(val)); // round half away from zero
        }

        ///< Convert integer to floating-point
        /// \param a is a signed integer value
        /// \param sizein is the number of bytes in the integer encoding
        /// \return a converted to an encoded floating-point value
        public ulong opInt2Float(ulong a, int sizein)
        {
            long ival = (long)a;
            Globals.sign_extend(ref ival, 8 * sizein - 1);
            // Convert integer to float
            double val = (double)ival;
            return getEncoding(val);
        }

        ///< Convert between floating-point precisions
        /// \param a is an encoded floating-point value
        /// \param outformat is the desired output FloatFormat
        /// \return a converted to the output FloatFormat
        public ulong opFloat2Float(ulong a, ref FloatFormat outformat)
        {
            return outformat.convertEncoding(a, this);
        }

        ///< Save the format to an XML stream
        /// Write the format out to a \<floatformat> XML tag.
        /// \param s is the output stream
        public void saveXml(StreamWriter s)
        {
            s.Write("<floatformat");
            Globals.a_v_i(s,"size", size);
            Globals.a_v_i(s,"signpos", signbit_pos);
            Globals.a_v_i(s,"fracpos", frac_pos);
            Globals.a_v_i(s,"fracsize", frac_size);
            Globals.a_v_i(s,"exppos", exp_pos);
            Globals.a_v_i(s,"expsize", exp_size);
            Globals.a_v_i(s,"bias", bias);
            Globals.a_v_b(s,"jbitimplied", jbitimplied);
            s.WriteLine("/>");
        }

        ///< Restore the format from XML
        /// Restore \b object from a \<floatformat> XML tag
        /// \param el is the element
        public void restoreXml(Element el)
        {
            using (TextReader s = new StringReader(el.getAttributeValue("size"))) {
                // s.unsetf(ios::dec | ios::hex | ios::oct);
                size = s.ReadDecimalInteger();
            }
            using (TextReader s = new StringReader(el.getAttributeValue("signpos"))) {
                // s.unsetf(ios::dec | ios::hex | ios::oct);
                signbit_pos = s.ReadDecimalInteger();
            }
            using (TextReader s = new StringReader(el.getAttributeValue("fracpos"))) {
                // s.unsetf(ios::dec | ios::hex | ios::oct);
                frac_pos = s.ReadDecimalInteger();
            }
            using (TextReader s = new StringReader(el.getAttributeValue("fracsize"))) {
                // s.unsetf(ios::dec | ios::hex | ios::oct);
                frac_size = s.ReadDecimalInteger();
            }
            using (TextReader s = new StringReader(el.getAttributeValue("exppos"))) {
                // s.unsetf(ios::dec | ios::hex | ios::oct);
                exp_pos = s.ReadDecimalInteger();
            }
            using (TextReader s = new StringReader(el.getAttributeValue("expsize"))) {
                // s.unsetf(ios::dec | ios::hex | ios::oct);
                exp_size = s.ReadDecimalInteger();
            }
            using (TextReader s = new StringReader(el.getAttributeValue("bias"))) {
                // s.unsetf(ios::dec | ios::hex | ios::oct);
                bias = s.ReadDecimalInteger();
            }
            jbitimplied = Globals.xml_readbool(el.getAttributeValue("jbitimplied"));
            maxexponent = (1 << exp_size) - 1;
            calcPrecision();
        }
    }
}
