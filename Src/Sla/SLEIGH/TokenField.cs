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
        private int bitstart;
        private int bitend;      // Bits within the token, 0 bit is LEAST significant
        private int bytestart;
        private int byteend;    // Bytes to read to get value
        private int shift;         // Amount to shift to align value  (bitstart % 8)
        
        public TokenField()
        {
        }
        
        public TokenField(Token tk, bool s, int bstart, int bend)
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

        public override long getValue(ParserWalker walker)
        {               // Construct value given specific instruction stream
            long res = getInstructionBytes(walker, bytestart, byteend, bigendian);

            res >>= shift;
            if (signbit)
                Globals.sign_extend(res, bitend - bitstart);
            else
                Globals.zero_extend(res, bitend - bitstart);
            return res;
        }

        public override TokenPattern genMinPattern(List<TokenPattern> ops) => TokenPattern(tok);

        public override TokenPattern genPattern(long val)
        {               // Generate corresponding pattern if the
                        // value is forced to be val
            return TokenPattern(tok, val, bitstart, bitend);
        }

        public override long minValue() => 0;

        public override long maxValue()
        {
            long res=0;
            res = ~res;
            Globals.zero_extend(res, bitend - bitstart);
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
            tok = (Token)null;
            bigendian = xml_readbool(el.getAttributeValue("bigendian"));
            signbit = xml_readbool(el.getAttributeValue("signbit"));
            {
                istringstream s = new istringstream(el.getAttributeValue("bitstart"));
                s.unsetf(ios::dec | ios::hex | ios::oct);
                s >> bitstart;
            }
            {
                istringstream s = new istringstream(el.getAttributeValue("bitend"));
                s.unsetf(ios::dec | ios::hex | ios::oct);
                s >> bitend;
            }
            {
                istringstream s = new istringstream(el.getAttributeValue("bytestart"));
                s.unsetf(ios::dec | ios::hex | ios::oct);
                s >> bytestart;
            }
            {
                istringstream s = new istringstream(el.getAttributeValue("byteend"));
                s.unsetf(ios::dec | ios::hex | ios::oct);
                s >> byteend;
            }
            {
                istringstream s = new istringstream(el.getAttributeValue("shift"));
                s.unsetf(ios::dec | ios::hex | ios::oct);
                s >> shift;
            }
        }
    }
}
