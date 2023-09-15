using Sla.CORE;

namespace Sla.SLEIGH
{
    internal class TokenField : PatternValue
    {
        private Token tok;
        private bool bigendian;
        private bool signbit;
        private int bitstart;
        // Bits within the token, 0 bit is LEAST significant
        private int bitend;
        private int bytestart;
        // Bytes to read to get value
        private int byteend;
        // Amount to shift to align value  (bitstart % 8)
        private int shift;
        
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
            if (tk.isBigEndian()) {
                byteend = (tk.getSize() * 8 - bitstart - 1) / 8;
                bytestart = (tk.getSize() * 8 - bitend - 1) / 8;
            }
            else {
                bytestart = bitstart / 8;
                byteend = bitend / 8;
            }
            shift = bitstart % 8;
        }

        public override long getValue(ParserWalker walker)
        {
            // Construct value given specific instruction stream
            long res = Globals.getInstructionBytes(walker, bytestart, byteend, bigendian);

            res >>= shift;
            if (signbit)
                Globals.sign_extend(ref res, bitend - bitstart);
            else
                Globals.zero_extend(ref res, bitend - bitstart);
            return res;
        }

        public override TokenPattern genMinPattern(List<TokenPattern> ops) => TokenPattern(tok);

        public override TokenPattern genPattern(long val)
        {
            // Generate corresponding pattern if the value is forced to be val
            return new TokenPattern(tok, val, bitstart, bitend);
        }

        public override long minValue() => 0;

        public override long maxValue()
        {
            long res=0;
            res = ~res;
            Globals.zero_extend(ref res, bitend - bitstart);
            return res;
        }

        public override void saveXml(TextWriter s)
        {
            s.Write("<tokenfield bigendian=\"");
            s.Write(bigendian ? "true\"" : "false\"");
            s.Write(" signbit=\"");
            s.Write(signbit ? "true\"" : "false\"");
            s.Write($" bitstart=\"{bitstart}\" bitend=\"{bitend}\" bytestart=\"{bytestart}\"");
            s.Write($" byteend=\"{byteend}\" shift=\"{shift}\"/>\n");
        }

        public override void restoreXml(Element el, Translate trans)
        {
            tok = (Token)null;
            bigendian = Xml.xml_readbool(el.getAttributeValue("bigendian"));
            signbit = Xml.xml_readbool(el.getAttributeValue("signbit"));
            bitstart = int.Parse(el.getAttributeValue("bitstart"));
            bitend = int.Parse(el.getAttributeValue("bitend"));
            bytestart = int.Parse(el.getAttributeValue("bytestart"));
            byteend = int.Parse(el.getAttributeValue("byteend"));
            shift = int.Parse(el.getAttributeValue("shift"));
        }
    }
}
