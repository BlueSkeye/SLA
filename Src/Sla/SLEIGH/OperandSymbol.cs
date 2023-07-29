using Sla.CORE;
using Sla.SLEIGH;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using static Sla.SLEIGH.SleighSymbol;

namespace Sla.SLEIGH
{
    internal class OperandSymbol : SpecificSymbol
    {
        //friend class Constructor;
        //friend class OperandEquation;
        [Flags()]
        public enum Flags
        {
            code_address = 1,
            offset_irrel = 2,
            variable_len = 4,
            marked = 8
        }
        
        private uint reloffset;      // Relative offset
        private int offsetbase;        // Base operand to which offset is relative (-1=constructor start)
        private int minimumlength;     // Minimum size of operand (within instruction tokens)
        private int hand;          // Handle index
        private OperandValue localexp;
        private TripleSymbol triple;       // Defining symbol
        private PatternExpression defexp;  // OR defining expression
        private uint flags;

        private void setVariableLength()
        {
            flags |= variable_len;
        }

        private bool isVariableLength() => ((flags&variable_len)!=0);

        public OperandSymbol()
        {
        }

        public OperandSymbol(string nm, int index, Constructor ct)
            : base(nm)
        {
            flags = 0;
            hand = index;
            localexp = new OperandValue(index, ct);
            localexp.layClaim();
            defexp = (PatternExpression*)0;
            triple = (TripleSymbol*)0;
        }

        public uint getRelativeOffset() => reloffset;

        public int getOffsetBase() => offsetbase;

        public int getMinimumLength() => minimumlength;

        public PatternExpression getDefiningExpression() => defexp;

        public TripleSymbol getDefiningSymbol() => triple;

        public int getIndex() => hand;

        public void defineOperand(PatternExpression pe)
        {
            if ((defexp != (PatternExpression*)0) || (triple != (TripleSymbol*)0))
                throw SleighError("Redefining operand");
            defexp = pe;
            defexp.layClaim();
        }

        public void defineOperand(TripleSymbol tri)
        {
            if ((defexp != (PatternExpression*)0) || (triple != (TripleSymbol*)0))
                throw SleighError("Redefining operand");
            triple = tri;
        }

        public void setCodeAddress()
        {
            flags |= code_address;
        }

        public bool isCodeAddress() => ((flags&code_address)!= 0);

        public void setOffsetIrrelevant()
        {
            flags |= offset_irrel;
        }

        public bool isOffsetIrrelevant() => ((flags&offset_irrel)!= 0);

        public void setMark()
        {
            flags |= marked;
        }

        public void clearMark()
        {
            flags &= ~((uint)marked);
        }

        public bool isMarked() => ((flags&marked)!= 0);

        ~OperandSymbol()
        {
            PatternExpression::release(localexp);
            if (defexp != (PatternExpression*)0)
                PatternExpression::release(defexp);
        }

        public override VarnodeTpl getVarnode()
        {
            VarnodeTpl* res;
            if (defexp != (PatternExpression*)0)
                res = new VarnodeTpl(hand, true); // Definite constant handle
            else
            {
                SpecificSymbol* specsym = dynamic_cast<SpecificSymbol*>(triple);
                if (specsym != (SpecificSymbol*)0)
                    res = specsym.getVarnode();
                else if ((triple != (TripleSymbol*)0) &&
                     ((triple.getType() == valuemap_symbol) || (triple.getType() == name_symbol)))
                    res = new VarnodeTpl(hand, true); // Zero-size symbols
                else
                    res = new VarnodeTpl(hand, false); // Possible dynamic handle
            }
            return res;
        }

        public override PatternExpression getPatternExpression() => localexp;

        public override void getFixedHandle(out FixedHandle hnd, ParserWalker walker)
        {
            hnd = walker.getFixedHandle(hand);
        }

        public override int getSize()
        {
            if (triple != (TripleSymbol*)0)
                return triple.getSize();
            return 0;
        }

        public override void print(TextWriter s, ParserWalker walker)
        {
            walker.pushOperand(getIndex());
            if (triple != (TripleSymbol*)0)
            {
                if (triple.getType() == SleighSymbol::subtable_symbol)
                    walker.getConstructor().print(s, walker);
                else
                    triple.print(s, walker);
            }
            else
            {
                long val = defexp.getValue(walker);
                if (val >= 0)
                    s << "0x" << hex << val;
                else
                    s << "-0x" << hex << -val;
            }
            walker.popOperand();
        }

        public override void collectLocalValues(List<ulong> results)
        {
            if (triple != (TripleSymbol*)0)
                triple.collectLocalValues(results);
        }

        public override symbol_type getType() => operand_symbol;

        public override void saveXml(TextWriter s)
        {
            s << "<operand_sym";
            SleighSymbol::saveXmlHeader(s);
            if (triple != (TripleSymbol*)0)
                s << " subsym=\"0x" << hex << triple.getId() << "\"";
            s << " off=\"" << dec << reloffset << "\"";
            s << " base=\"" << offsetbase << "\"";
            s << " minlen=\"" << minimumlength << "\"";
            if (isCodeAddress())
                s << " code=\"true\"";
            s << " index=\"" << dec << hand << "\">\n";
            localexp.saveXml(s);
            if (defexp != (PatternExpression*)0)
                defexp.saveXml(s);
            s << "</operand_sym>\n";
        }

        public override void saveXmlHeader(TextWriter s)
        {
            s << "<operand_sym_head";
            SleighSymbol::saveXmlHeader(s);
            s << "/>\n";
        }

        public override void restoreXml(Element el, SleighBase trans)
        {
            defexp = (PatternExpression*)0;
            triple = (TripleSymbol*)0;
            flags = 0;
            {
                istringstream s(el.getAttributeValue("index"));
                s.unsetf(ios::dec | ios::hex | ios::oct);
                s >> hand;
            }
            {
                istringstream s(el.getAttributeValue("off"));
                s.unsetf(ios::dec | ios::hex | ios::oct);
                s >> reloffset;
            }
            {
                istringstream s(el.getAttributeValue("base"));
                s.unsetf(ios::dec | ios::hex | ios::oct);
                s >> offsetbase;
            }
            {
                istringstream s(el.getAttributeValue("minlen"));
                s.unsetf(ios::dec | ios::hex | ios::oct);
                s >> minimumlength;
            }
            for (int i = 0; i < el.getNumAttributes(); ++i)
            {
                if (el.getAttributeName(i) == "subsym")
                {
                    uint id;
                    istringstream s(el.getAttributeValue(i));
                    s.unsetf(ios::dec | ios::hex | ios::oct);
                    s >> id;
                    triple = (TripleSymbol*)trans.findSymbol(id);
                }
                else if (el.getAttributeName(i) == "code")
                {
                    if (xml_readbool(el.getAttributeValue(i)))
                        flags |= code_address;
                }
            }
            List list = el.getChildren();
            List::const_iterator iter;
            iter = list.begin();
            localexp = (OperandValue*)PatternExpression::restoreExpression(*iter, trans);
            localexp.layClaim();
            ++iter;
            if (iter != list.end())
            {
                defexp = PatternExpression::restoreExpression(*iter, trans);
                defexp.layClaim();
            }
        }
    }
}
