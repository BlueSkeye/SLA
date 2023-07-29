using Sla.CORE;
using Sla.SLEIGH;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Text;
using System.Threading.Tasks;

namespace Sla.SLEIGH
{
    internal class Constructor
    {
        // This is NOT a symbol
        private TokenPattern pattern;
        private SubtableSymbol parent;
        private PatternEquation pateq;
        private List<OperandSymbol> operands;
        private List<string> printpiece;
        private List<ContextChange> context; // Context commands
        private ConstructTpl templ;        // The main p-code section
        private List<ConstructTpl> namedtempl; // Other named p-code sections
        private int minimumlength;     // Minimum length taken up by this constructor in bytes
        private uint id;           // Unique id of constructor within subtable
        private int firstwhitespace;       // Index of first whitespace piece in -printpiece-
        private int flowthruindex;     // if >=0 then print only a single operand no markup
        private int lineno;
        private int src_index;           //source file index
        private /*mutable*/ bool inerror;                 // An error is associated with this Constructor

        private void orderOperands()
        {
            OperandSymbol* sym;
            List<OperandSymbol*> patternorder;
            List<OperandSymbol*> newops; // New order of the operands
            int lastsize;

            pateq.operandOrder(this, patternorder);
            for (int i = 0; i < operands.size(); ++i)
            { // Make sure patternorder contains all operands
                sym = operands[i];
                if (!sym.isMarked())
                {
                    patternorder.Add(sym);
                    sym.setMark();     // Make sure all operands are marked
                }
            }
            do
            {
                lastsize = newops.size();
                for (int i = 0; i < patternorder.size(); ++i)
                {
                    sym = patternorder[i];
                    if (!sym.isMarked()) continue; // "unmarked" means it is already in newops
                    if (sym.isOffsetIrrelevant()) continue; // expression Operands come last
                    if ((sym.offsetbase == -1) || (!operands[sym.offsetbase].isMarked()))
                    {
                        newops.Add(sym);
                        sym.clearMark();
                    }
                }
            } while (newops.size() != lastsize);
            for (int i = 0; i < patternorder.size(); ++i)
            { // Tack on expression Operands
                sym = patternorder[i];
                if (sym.isOffsetIrrelevant())
                {
                    newops.Add(sym);
                    sym.clearMark();
                }
            }

            if (newops.size() != operands.size())
                throw new SleighError("Circular offset dependency between operands");


            for (int i = 0; i < newops.size(); ++i)
            { // Fix up operand indices
                newops[i].hand = i;
                newops[i].localexp.changeIndex(i);
            }
            List<int> handmap;       // Create index translation map
            for (int i = 0; i < operands.size(); ++i)
                handmap.Add(operands[i].hand);

            // Fix up offsetbase
            for (int i = 0; i < newops.size(); ++i)
            {
                sym = newops[i];
                if (sym.offsetbase == -1) continue;
                sym.offsetbase = handmap[sym.offsetbase];
            }

            if (templ != (ConstructTpl)null) // Fix up templates
                templ.changeHandleIndex(handmap);
            for (int i = 0; i < namedtempl.size(); ++i)
            {
                ConstructTpl* ntempl = namedtempl[i];
                if (ntempl != (ConstructTpl)null)
                    ntempl.changeHandleIndex(handmap);
            }

            // Fix up printpiece operand refs
            for (int i = 0; i < printpiece.size(); ++i)
            {
                if (printpiece[i][0] == '\n')
                {
                    int index = printpiece[i][1] - 'A';
                    index = handmap[index];
                    printpiece[i][1] = 'A' + index;
                }
            }
            operands = newops;
        }

        public Constructor()
        {
            pattern = (TokenPattern)null;
            parent = (SubtableSymbol*)0;
            pateq = (PatternEquation*)0;
            templ = (ConstructTpl)null;
            firstwhitespace = -1;
            flowthruindex = -1;
            inerror = false;
        }

        public Constructor(SubtableSymbol p)
        {
            pattern = (TokenPattern)null;
            parent = p;
            pateq = (PatternEquation*)0;
            templ = (ConstructTpl)null;
            firstwhitespace = -1;
            inerror = false;
        }

        ~Constructor()
        {
            if (pattern != (TokenPattern)null)
                delete pattern;
            if (pateq != (PatternEquation*)0)
                PatternEquation::release(pateq);
            if (templ != (ConstructTpl)null)
                delete templ;
            for (int i = 0; i < namedtempl.size(); ++i)
            {
                ConstructTpl* ntpl = namedtempl[i];
                if (ntpl != (ConstructTpl)null)
                    delete ntpl;
            }
            List<ContextChange*>::iterator iter;
            for (iter = context.begin(); iter != context.end(); ++iter)
                delete* iter;
        }

        public TokenPattern buildPattern(TextWriter s)
        {
            if (pattern != (TokenPattern)null) return pattern; // Already built

            pattern = new TokenPattern();
            List<TokenPattern> oppattern;
            bool recursion = false;
            // Generate pattern for each operand, store in oppattern
            for (int i = 0; i < operands.size(); ++i)
            {
                OperandSymbol* sym = operands[i];
                TripleSymbol* triple = sym.getDefiningSymbol();
                PatternExpression* defexp = sym.getDefiningExpression();
                if (triple != (TripleSymbol)null)
                {
                    SubtableSymbol* subsym = dynamic_cast<SubtableSymbol*>(triple);
                    if (subsym != (SubtableSymbol*)0)
                    {
                        if (subsym.isBeingBuilt())
                        { // Detected recursion
                            if (recursion)
                            {
                                throw new SleighError("Illegal recursion");
                            }
                            // We should also check that recursion is rightmost extreme
                            recursion = true;
                            oppattern.emplace_back();
                        }
                        else
                            oppattern.Add(*subsym.buildPattern(s));
                    }
                    else
                        oppattern.Add(triple.getPatternExpression().genMinPattern(oppattern));
                }
                else if (defexp != (PatternExpression)null)
                    oppattern.Add(defexp.genMinPattern(oppattern));
                else
                {
                    throw new SleighError(sym.getName() + ": operand is undefined");
                }
                TokenPattern & sympat(oppattern.back());
                sym.minimumlength = sympat.getMinimumLength();
                if (sympat.getLeftEllipsis() || sympat.getRightEllipsis())
                    sym.setVariableLength();
            }

            if (pateq == (PatternEquation*)0)
                throw new SleighError("Missing equation");

            // Build the entire pattern
            pateq.genPattern(oppattern);
            *pattern = pateq.getTokenPattern();
            if (pattern.alwaysFalse())
                throw new SleighError("Impossible pattern");
            if (recursion)
                pattern.setRightEllipsis(true);
            minimumlength = pattern.getMinimumLength(); // Get length of the pattern in bytes

            // Resolve offsets of the operands
            OperandResolve resolve(operands);
            if (!pateq.resolveOperandLeft(resolve))
                throw new SleighError("Unable to resolve operand offsets");

            for (int i = 0; i < operands.size(); ++i)
            { // Unravel relative offsets to absolute (if possible)
                int @base;
                int offset;
                OperandSymbol sym = operands[i];
                if (sym.isOffsetIrrelevant())
                {
                    sym.offsetbase = -1;
                    sym.reloffset = 0;
                    continue;
                }
                @base = sym.offsetbase;
                offset = sym.reloffset;
                while (@base >= 0)
                {
                    sym = operands[@base];
                    if (sym.isVariableLength()) break; // Cannot resolve to absolute
                    @base = sym.offsetbase;
                    offset += sym.getMinimumLength();
                    offset += sym.reloffset;
                    if (@base < 0)
                    {
                        operands[i].offsetbase = @base;
                        operands[i].reloffset = offset;
                    }
                }
            }

            // Make sure context expressions are valid
            for (int i = 0; i < context.size(); ++i)
                context[i].validate();

            orderOperands();        // Order the operands based on offset dependency
            return pattern;
        }

        public TokenPattern getPattern() => pattern;

        public void setMinimumLength(int l)
        {
            minimumlength = l;
        }

        public int getMinimumLength() => minimumlength;

        public void setId(uint i)
        {
            id = i;
        }

        public uint getId() => id;

        public void setLineno(int ln)
        {
            lineno = ln;
        }

        public int getLineno() => lineno;

        public void setSrcIndex(int index)
        {
            src_index = index;
        }

        public int getSrcIndex() => src_index;

        public void addContext(List<ContextChange> vec)
        {
            context = vec;
        }

        public void addOperand(OperandSymbol sym)
        {
            string operstring = "\n ";  // Indicater character for operand
            operstring[1] = ('A' + operands.size()); // Encode index of operand
            operands.Add(sym);
            printpiece.Add(operstring); // Placeholder for operand's string
        }

        public void addInvisibleOperand(OperandSymbol sym)
        {
            operands.Add(sym);
        }

        public void addSyntax(string syn)
        {
            string syntrim;

            if (syn.size() == 0) return;
            bool hasNonSpace = false;
            for (int i = 0; i < syn.size(); ++i)
            {
                if (syn[i] != ' ')
                {
                    hasNonSpace = true;
                    break;
                }
            }
            if (hasNonSpace)
                syntrim = syn;
            else
                syntrim = " ";
            if ((firstwhitespace == -1) && (syntrim == " "))
                firstwhitespace = printpiece.size();
            if (printpiece.empty())
                printpiece.Add(syntrim);
            else if (printpiece.back() == " " && syntrim == " ")
            {
                // Don't add more whitespace
            }
            else if (printpiece.back()[0] == '\n' || printpiece.back() == " " || syntrim == " ")
                printpiece.Add(syntrim);
            else
            {
                printpiece.back() += syntrim;
            }
        }

        public void addEquation(PatternEquation pe)
        {
            (pateq = pe).layClaim();
        }

        public void setMainSection(ConstructTpl tpl)
        {
            templ = tpl;
        }

        public void setNamedSection(ConstructTpl tpl, int id)
        {               // Add a named section to the constructor
            while (namedtempl.size() <= id)
                namedtempl.Add((ConstructTpl)null);
            namedtempl[id] = tpl;
        }

        public SubtableSymbol getParent() => parent;

        public int getNumOperands() => operands.size();

        public OperandSymbol getOperand(int i) => operands[i];

        public PatternEquation getPatternEquation() => pateq;

        public ConstructTpl getTempl() => templ;

        public ConstructTpl getNamedTempl(int secnum)
        {
            if (secnum < namedtempl.size())
                return namedtempl[secnum];
            return (ConstructTpl)null;
        }

        public int getNumSections() => namedtempl.size();

        public void printInfo(TextWriter s)
        {               // Print identifying information about constructor
                        // for use in error messages
            s << "table \"" << parent.getName();
            s << "\" constructor starting at line " << dec << lineno;
        }

        public void print(TextWriter s, ParserWalker pos)
        {
            List<string>::const_iterator piter;

            for (piter = printpiece.begin(); piter != printpiece.end(); ++piter)
            {
                if ((*piter)[0] == '\n')
                {
                    int index = (*piter)[1] - 'A';
                    operands[index].print(s, walker);
                }
                else
                    s << *piter;
            }
        }

        public void printMnemonic(TextWriter s, ParserWalker walker)
        {
            if (flowthruindex != -1)
            {
                SubtableSymbol* sym = dynamic_cast<SubtableSymbol*>(operands[flowthruindex].getDefiningSymbol());
                if (sym != (SubtableSymbol*)0)
                {
                    walker.pushOperand(flowthruindex);
                    walker.getConstructor().printMnemonic(s, walker);
                    walker.popOperand();
                    return;
                }
            }
            int endind = (firstwhitespace == -1) ? printpiece.size() : firstwhitespace;
            for (int i = 0; i < endind; ++i)
            {
                if (printpiece[i][0] == '\n')
                {
                    int index = printpiece[i][1] - 'A';
                    operands[index].print(s, walker);
                }
                else
                    s << printpiece[i];
            }
        }

        public void printBody(TextWriter s, ParserWalker walker)
        {
            if (flowthruindex != -1)
            {
                SubtableSymbol* sym = dynamic_cast<SubtableSymbol*>(operands[flowthruindex].getDefiningSymbol());
                if (sym != (SubtableSymbol*)0)
                {
                    walker.pushOperand(flowthruindex);
                    walker.getConstructor().printBody(s, walker);
                    walker.popOperand();
                    return;
                }
            }
            if (firstwhitespace == -1) return; // Nothing to print after firstwhitespace
            for (int i = firstwhitespace + 1; i < printpiece.size(); ++i)
            {
                if (printpiece[i][0] == '\n')
                {
                    int index = printpiece[i][1] - 'A';
                    operands[index].print(s, walker);
                }
                else
                    s << printpiece[i];
            }
        }

        public void removeTrailingSpace()
        {
            // Allow for user to force extra space at end of printing
            if ((!printpiece.empty()) && (printpiece.back() == " "))
                printpiece.pop_back();
            //  while((!printpiece.empty())&&(printpiece.back()==" "))
            //    printpiece.pop_back();
        }

        public void applyContext(ParserWalkerChange walker)
        {
            List<ContextChange*>::const_iterator iter;
            for (iter = context.begin(); iter != context.end(); ++iter)
                (*iter).apply(walker);
        }

        public void markSubtableOperands(List<int> check)
        { // Adjust -check- so it has one entry for every operand, a 0 if it is a subtable, a 2 if it is not
            check.resize(operands.size());
            for (int i = 0; i < operands.size(); ++i)
            {
                TripleSymbol* sym = operands[i].getDefiningSymbol();
                if ((sym != (TripleSymbol)null) && (sym.getType() == SleighSymbol::subtable_symbol))
                    check[i] = 0;
                else
                    check[i] = 2;
            }
        }

        public void collectLocalExports(List<ulong> results)
        {
            if (templ == (ConstructTpl)null) return;
            HandleTpl* handle = templ.getResult();
            if (handle == (HandleTpl)null) return;
            if (handle.getSpace().isConstSpace()) return;  // Even if the value is dynamic, the pointed to value won't get used
            if (handle.getPtrSpace().getType() != ConstTpl::real)
            {
                if (handle.getTempSpace().isUniqueSpace())
                    results.Add(handle.getTempOffset().getReal());
                return;
            }
            if (handle.getSpace().isUniqueSpace())
            {
                results.Add(handle.getPtrOffset().getReal());
                return;
            }
            if (handle.getSpace().getType() == ConstTpl::handle)
            {
                int handleIndex = handle.getSpace().getHandleIndex();
                OperandSymbol* opSym = getOperand(handleIndex);
                opSym.collectLocalValues(results);
            }
        }

        public void setError(bool val)
        {
            inerror = val;
        }

        public bool isError() => inerror;

        public bool isRecursive()
        { // Does this constructor cause recursion with its table
            for (int i = 0; i < operands.size(); ++i)
            {
                TripleSymbol* sym = operands[i].getDefiningSymbol();
                if (sym == parent) return true;
            }
            return false;
        }

        public void saveXml(TextWriter s)
        {
            s << "<constructor";
            s << " parent=\"0x" << hex << parent.getId() << "\"";
            s << " first=\"" << dec << firstwhitespace << "\"";
            s << " length=\"" << minimumlength << "\"";
            s << " line=\"" << src_index << ":" << lineno << "\">\n";
            for (int i = 0; i < operands.size(); ++i)
                s << "<oper id=\"0x" << hex << operands[i].getId() << "\"/>\n";
            for (int i = 0; i < printpiece.size(); ++i)
            {
                if (printpiece[i][0] == '\n')
                {
                    int index = printpiece[i][1] - 'A';
                    s << "<opprint id=\"" << dec << index << "\"/>\n";
                }
                else
                {
                    s << "<print piece=\"";
                    xml_escape(s, printpiece[i].c_str());
                    s << "\"/>\n";
                }
            }
            for (int i = 0; i < context.size(); ++i)
                context[i].saveXml(s);
            if (templ != (ConstructTpl)null)
                templ.saveXml(s, -1);
            for (int i = 0; i < namedtempl.size(); ++i)
            {
                if (namedtempl[i] == (ConstructTpl)null) // Some sections may be NULL
                    continue;
                namedtempl[i].saveXml(s, i);
            }
            s << "</constructor>\n";
        }

        public void restoreXml(Element el, SleighBase trans)
        {
            uint id;
            {
                istringstream s = new istringstream(el.getAttributeValue("parent"));
                s.unsetf(ios::dec | ios::hex | ios::oct);
                s >> id;
                parent = (SubtableSymbol)trans.findSymbol(id);
            }
            {
                istringstream s = new istringstream(el.getAttributeValue("first"));
                s.unsetf(ios::dec | ios::hex | ios::oct);
                s >> firstwhitespace;
            }
            {
                istringstream s = new istringstream(el.getAttributeValue("length"));
                s.unsetf(ios::dec | ios::hex | ios::oct);
                s >> minimumlength;
            }
            {
                string src_and_line = el.getAttributeValue("line");
                size_t pos = src_and_line.find(":");
                src_index = stoi(src_and_line.substr(0, pos), NULL, 10);
                lineno = stoi(src_and_line.substr(pos + 1, src_and_line.length()), NULL, 10);
            }
            List list = el.getChildren();
            List::const_iterator iter;
            iter = list.begin();
            while (iter != list.end())
            {
                if ((*iter).getName() == "oper")
                {
                    uint id;
                    {
                        istringstream s = new istringstream((* iter).getAttributeValue("id"));
                        s.unsetf(ios::dec | ios::hex | ios::oct);
                        s >> id;
                    }
                    OperandSymbol sym = (OperandSymbol)trans.findSymbol(id);
                    operands.Add(sym);
                }
                else if ((*iter).getName() == "print")
                    printpiece.Add((*iter).getAttributeValue("piece"));
                else if ((*iter).getName() == "opprint")
                {
                    int index;
                    istringstream s = new istringstream((* iter).getAttributeValue("id"));
                    s.unsetf(ios::dec | ios::hex | ios::oct);
                    s >> index;
                    string operstring = "\n ";
                    operstring[1] = ('A' + index);
                    printpiece.Add(operstring);
                }
                else if ((*iter).getName() == "context_op")
                {
                    ContextOp c_op = new ContextOp();
                    c_op.restoreXml(*iter, trans);
                    context.Add(c_op);
                }
                else if ((*iter).getName() == "commit")
                {
                    ContextCommit c_op = new ContextCommit();
                    c_op.restoreXml(*iter, trans);
                    context.Add(c_op);
                }
                else
                {
                    ConstructTpl cur = new ConstructTpl();
                    int sectionid = cur.restoreXml(*iter, trans);
                    if (sectionid < 0)
                    {
                        if (templ != (ConstructTpl)null)
                            throw new LowlevelError("Duplicate main section");
                        templ = cur;
                    }
                    else
                    {
                        while (namedtempl.size() <= sectionid)
                            namedtempl.Add((ConstructTpl)null);
                        if (namedtempl[sectionid] != (ConstructTpl)null)
                            throw new LowlevelError("Duplicate named section");
                        namedtempl[sectionid] = cur;
                    }
                }
                ++iter;
            }
            pattern = (TokenPattern)null;
            if ((printpiece.size() == 1) && (printpiece[0][0] == '\n'))
                flowthruindex = printpiece[0][1] - 'A';
            else
                flowthruindex = -1;
        }
    }
}
