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
        private int4 startbit;
        private int4 endbit;
        private int4 startbyte;
        private int4 endbyte;
        private int4 shift;
        private bool signbit;
        
        public ContextField()
        {
        }

        public ContextField(bool s, int4 sbit, int4 ebit)
        {
            signbit = s;
            startbit = sbit;
            endbit = ebit;
            startbyte = startbit / 8;
            endbyte = endbit / 8;
            shift = 7 - (endbit % 8);
        }

        public int4 getStartBit() => startbit;

        public int4 getEndBit() => endbit;

        public bool getSignBit() => signbit;

        public override intb getValue(ParserWalker walker)
        {
            intb res = getContextBytes(walker, startbyte, endbyte);
            res >>= shift;
            if (signbit)
                sign_extend(res, endbit - startbit);
            else
                zero_extend(res, endbit - startbit);
            return res;
        }

        public override TokenPattern genMinPattern(List<TokenPattern> ops) => new TokenPattern();

        public override TokenPattern genPattern(intb val)
        {
            return TokenPattern(val, startbit, endbit);
        }

        public override intb minValue() => 0;

        public override intb maxValue()
        {
            intb res=0;
            res = ~res;
            zero_extend(res, (endbit - startbit));
            return res;
        }

        public override void saveXml(TextWriter s)
        {
            s << "<contextfield";
            s << " signbit=\"";
            if (signbit)
                s << "true\"";
            else
                s << "false\"";
            s << " startbit=\"" << dec << startbit << "\"";
            s << " endbit=\"" << endbit << "\"";
            s << " startbyte=\"" << startbyte << "\"";
            s << " endbyte=\"" << endbyte << "\"";
            s << " shift=\"" << shift << "\"/>\n";
        }

        public override void restoreXml(Element el, Translate trans)
        {
            signbit = xml_readbool(el->getAttributeValue("signbit"));
            {
                istringstream s(el->getAttributeValue("startbit"));
                s.unsetf(ios::dec | ios::hex | ios::oct);
                s >> startbit;
            }
            {
                istringstream s(el->getAttributeValue("endbit"));
                s.unsetf(ios::dec | ios::hex | ios::oct);
                s >> endbit;
            }
            {
                istringstream s(el->getAttributeValue("startbyte"));
                s.unsetf(ios::dec | ios::hex | ios::oct);
                s >> startbyte;
            }
            {
                istringstream s(el->getAttributeValue("endbyte"));
                s.unsetf(ios::dec | ios::hex | ios::oct);
                s >> endbyte;
            }
            {
                istringstream s(el->getAttributeValue("shift"));
                s.unsetf(ios::dec | ios::hex | ios::oct);
                s >> shift;
            }
        }
    }
}
