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
    internal class OperandValue : PatternValue
    {
        private int4 index;         // This is the defining field of expression
        private Constructor ct;        // cached pointer to constructor
        
        public OperandValue()
        {
        } // For use with restoreXml

        public OperandValue(int4 ind, Constructor c)
        {
            index = ind;
            ct = c;
        }

        public void changeIndex(int4 newind)
        {
            index = newind;
        }

        public bool isConstructorRelative()
        {
            OperandSymbol* sym = ct->getOperand(index);
            return (sym->getOffsetBase() == -1);
        }

        public string getName()
        {
            OperandSymbol* sym = ct->getOperand(index);
            return sym->getName();
        }

        public override TokenPattern genPattern(intb val);

        public override TokenPattern genMinPattern(List<TokenPattern> ops) => ops[index];

        public override intb getValue(ParserWalker walker)
        {               // Get the value of an operand when it is used in
                        // an expression. 
            OperandSymbol* sym = ct->getOperand(index);
            PatternExpression* patexp = sym->getDefiningExpression();
            if (patexp == (PatternExpression*)0)
            {
                TripleSymbol* defsym = sym->getDefiningSymbol();
                if (defsym != (TripleSymbol*)0)
                    patexp = defsym->getPatternExpression();
                if (patexp == (PatternExpression*)0)
                    return 0;
            }
            ConstructState tempstate;
            ParserWalker newwalker(walker.getParserContext());
            newwalker.setOutOfBandState(ct, index, &tempstate, walker);
            intb res = patexp->getValue(newwalker);
            return res;
        }

        public override intb getSubValue(List<intb> replace, int4 listpos)
        {
            OperandSymbol* sym = ct->getOperand(index);
            return sym->getDefiningExpression()->getSubValue(replace, listpos);
        }

        public override intb minValue()
        {
            throw SleighError("Operand used in pattern expression");
        }

        public override intb maxValue()
        {
            throw SleighError("Operand used in pattern expression");
        }

        public override void saveXml(TextWriter s)
        {
            s << "<operand_exp";
            s << " index=\"" << dec << index << "\"";
            s << " table=\"0x" << hex << ct->getParent()->getId() << "\"";
            s << " ct=\"0x" << ct->getId() << "\"/>\n"; // Save id of our constructor
        }

        public override void restoreXml(Element el, Translate trans)
        {
            uintm ctid, tabid;
            {
                istringstream s(el->getAttributeValue("index"));
                s.unsetf(ios::dec | ios::hex | ios::oct);
                s >> index;
            }
            {
                istringstream s(el->getAttributeValue("table"));
                s.unsetf(ios::dec | ios::hex | ios::oct);
                s >> tabid;
            }
            {
                istringstream s(el->getAttributeValue("ct"));
                s.unsetf(ios::dec | ios::hex | ios::oct);
                s >> ctid;
            }
            SleighBase* sleigh = (SleighBase*)trans;
            SubtableSymbol* tab = dynamic_cast<SubtableSymbol*>(sleigh->findSymbol(tabid));
            ct = tab->getConstructor(ctid);
        }
    }
}
