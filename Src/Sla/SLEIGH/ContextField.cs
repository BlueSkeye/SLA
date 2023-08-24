using Sla.CORE;
using Sla.SLEIGH;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Sla.SLEIGH
{
    internal class ContextField : PatternValue
    {
        private int startbit;
        private int endbit;
        private int startbyte;
        private int endbyte;
        private int shift;
        private bool signbit;
        
        public ContextField()
        {
        }

        public ContextField(bool s, int sbit, int ebit)
        {
            signbit = s;
            startbit = sbit;
            endbit = ebit;
            startbyte = startbit / 8;
            endbyte = endbit / 8;
            shift = 7 - (endbit % 8);
        }

        public int getStartBit() => startbit;

        public int getEndBit() => endbit;

        public bool getSignBit() => signbit;

        public override long getValue(ParserWalker walker)
        {
            long res = getContextBytes(walker, startbyte, endbyte);
            res >>= shift;
            if (signbit)
                Globals.sign_extend(ref res, endbit - startbit);
            else
                Globals.zero_extend(ref res, endbit - startbit);
            return res;
        }

        public override TokenPattern genMinPattern(List<TokenPattern> ops) => new TokenPattern();

        public override TokenPattern genPattern(long val)
        {
            return TokenPattern(val, startbit, endbit);
        }

        public override long minValue() => 0;

        public override long maxValue()
        {
            long res=0;
            res = ~res;
            Globals.zero_extend(ref res, (endbit - startbit));
            return res;
        }

        public override void saveXml(TextWriter s)
        {
            s.Write("<contextfield");
            s.Write(" signbit=\"");
            if (signbit)
                s.Write("true\"");
            else
                s.Write("false\"");
            s.Write($" startbit=\"{startbit}\"");
            s.Write($" endbit=\"{endbit}\"");
            s.Write($" startbyte=\"{startbyte}\"");
            s.Write($" endbyte=\"{endbyte}\"");
            s.WriteLine(" shift=\"{shift}\"/>");
        }

        public override void restoreXml(Element el, Translate trans)
        {
            signbit = Xml.xml_readbool(el.getAttributeValue("signbit"));
            startbit = int.Parse(el.getAttributeValue("startbit"));
            endbit = int.Parse(el.getAttributeValue("endbit"));
            startbyte = int.Parse(el.getAttributeValue("startbyte"));
            endbyte = int.Parse(el.getAttributeValue("endbyte"));
            shift = int.Parse(el.getAttributeValue("shift"));
        }
    }
}
