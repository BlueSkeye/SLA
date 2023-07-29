using Sla.CORE;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Sla.SLEIGH
{
    internal class TokenField : PatternValue
    {
        private Token tok;
        private bool bigendian;
        private bool signbit;
        private int4 bitstart;
        private int4 bitend;      // Bits within the token, 0 bit is LEAST significant
        private int4 bytestart;
        private int4 byteend;    // Bytes to read to get value
        private int4 shift;         // Amount to shift to align value  (bitstart % 8)
        
        public TokenField()
        {
        }
        
        public TokenField(Token tk, bool s, int4 bstart, int4 bend)
        {
            tok = tk;
            bigendian = tok.isBigEndian();
            signbit = s;
            bitstart = bstart;
            bitend = bend;
            if (tk.isBigEndian())
            {
                byteend = (tk.getSize() * 8 - bitstart - 1) / 8;
                bytestart = (tk.getSize() * 8 - bitend - 1) / 8;
            }
            else
            {
                bytestart = bitstart / 8;
                byteend = bitend / 8;
            }
            shift = bitstart % 8;
        }

        public override intb getValue(ParserWalker walker)
        {               // Construct value given specific instruction stream
            intb res = getInstructionBytes(walker, bytestart, byteend, bigendian);

            res >>= shift;
            if (signbit)
                sign_extend(res, bitend - bitstart);
            else
                zero_extend(res, bitend - bitstart);
            return res;
        }

        public override TokenPattern genMinPattern(List<TokenPattern> ops) => TokenPattern(tok);

        public override TokenPattern genPattern(intb val)
        {               // Generate corresponding pattern if the
                        // value is forced to be val
            return TokenPattern(tok, val, bitstart, bitend);
        }

        public override intb minValue() => 0;

        public override intb maxValue()
        {
            intb res=0;
            res = ~res;
            zero_extend(res, bitend - bitstart);
            return res;
        }

        public override void saveXml(TextWriter s)
        {
            s << "<tokenfield";
            s << " bigendian=\"";
            if (bigendian)
                s << "true\"";
            else
                s << "false\"";
            s << " signbit=\"";
            if (signbit)
                s << "true\"";
            else
                s << "false\"";
            s << " bitstart=\"" << dec << bitstart << "\"";
            s << " bitend=\"" << bitend << "\"";
            s << " bytestart=\"" << bytestart << "\"";
            s << " byteend=\"" << byteend << "\"";
            s << " shift=\"" << shift << "\"/>\n";
        }

        public override void restoreXml(Element el, Translate trans)
        {
            tok = (Token*)0;
            bigendian = xml_readbool(el.getAttributeValue("bigendian"));
            signbit = xml_readbool(el.getAttributeValue("signbit"));
            {
                istringstream s(el.getAttributeValue("bitstart"));
                s.unsetf(ios::dec | ios::hex | ios::oct);
                s >> bitstart;
            }
            {
                istringstream s(el.getAttributeValue("bitend"));
                s.unsetf(ios::dec | ios::hex | ios::oct);
                s >> bitend;
            }
            {
                istringstream s(el.getAttributeValue("bytestart"));
                s.unsetf(ios::dec | ios::hex | ios::oct);
                s >> bytestart;
            }
            {
                istringstream s(el.getAttributeValue("byteend"));
                s.unsetf(ios::dec | ios::hex | ios::oct);
                s >> byteend;
            }
            {
                istringstream s(el.getAttributeValue("shift"));
                s.unsetf(ios::dec | ios::hex | ios::oct);
                s >> shift;
            }
        }
    }
}
