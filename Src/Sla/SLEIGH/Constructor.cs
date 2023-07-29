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
        private int4 minimumlength;     // Minimum length taken up by this constructor in bytes
        private uintm id;           // Unique id of constructor within subtable
        private int4 firstwhitespace;       // Index of first whitespace piece in -printpiece-
        private int4 flowthruindex;     // if >=0 then print only a single operand no markup
        private int4 lineno;
        private int4 src_index;           //source file index
        private /*mutable*/ bool inerror;                 // An error is associated with this Constructor

        private void orderOperands()
        {
            OperandSymbol* sym;
            vector<OperandSymbol*> patternorder;
            vector<OperandSymbol*> newops; // New order of the operands
            int4 lastsize;

            pateq->operandOrder(this, patternorder);
            for (int4 i = 0; i < operands.size(); ++i)
            { // Make sure patternorder contains all operands
                sym = operands[i];
                if (!sym->isMarked())
                {
                    patternorder.push_back(sym);
                    sym->setMark();     // Make sure all operands are marked
                }
            }
            do
            {
                lastsize = newops.size();
                for (int4 i = 0; i < patternorder.size(); ++i)
                {
                    sym = patternorder[i];
                    if (!sym->isMarked()) continue; // "unmarked" means it is already in newops
                    if (sym->isOffsetIrrelevant()) continue; // expression Operands come last
                    if ((sym->offsetbase == -1) || (!operands[sym->offsetbase]->isMarked()))
                    {
                        newops.push_back(sym);
                        sym->clearMark();
                    }
                }
            } while (newops.size() != lastsize);
            for (int4 i = 0; i < patternorder.size(); ++i)
            { // Tack on expression Operands
                sym = patternorder[i];
                if (sym->isOffsetIrrelevant())
                {
                    newops.push_back(sym);
                    sym->clearMark();
                }
            }

            if (newops.size() != operands.size())
                throw SleighError("Circular offset dependency between operands");


            for (int4 i = 0; i < newops.size(); ++i)
            { // Fix up operand indices
                newops[i]->hand = i;
                newops[i]->localexp->changeIndex(i);
            }
            vector<int4> handmap;       // Create index translation map
            for (int4 i = 0; i < operands.size(); ++i)
                handmap.push_back(operands[i]->hand);

            // Fix up offsetbase
            for (int4 i = 0; i < newops.size(); ++i)
            {
                sym = newops[i];
                if (sym->offsetbase == -1) continue;
                sym->offsetbase = handmap[sym->offsetbase];
            }

            if (templ != (ConstructTpl*)0) // Fix up templates
                templ->changeHandleIndex(handmap);
            for (int4 i = 0; i < namedtempl.size(); ++i)
            {
                ConstructTpl* ntempl = namedtempl[i];
                if (ntempl != (ConstructTpl*)0)
                    ntempl->changeHandleIndex(handmap);
            }

            // Fix up printpiece operand refs
            for (int4 i = 0; i < printpiece.size(); ++i)
            {
                if (printpiece[i][0] == '\n')
                {
                    int4 index = printpiece[i][1] - 'A';
                    index = handmap[index];
                    printpiece[i][1] = 'A' + index;
                }
            }
            operands = newops;
        }

        public Constructor()
        {
            pattern = (TokenPattern*)0;
            parent = (SubtableSymbol*)0;
            pateq = (PatternEquation*)0;
            templ = (ConstructTpl*)0;
            firstwhitespace = -1;
            flowthruindex = -1;
            inerror = false;
        }

        public Constructor(SubtableSymbol p)
        {
            pattern = (TokenPattern*)0;
            parent = p;
            pateq = (PatternEquation*)0;
            templ = (ConstructTpl*)0;
            firstwhitespace = -1;
            inerror = false;
        }

        ~Constructor()
        {
            if (pattern != (TokenPattern*)0)
                delete pattern;
            if (pateq != (PatternEquation*)0)
                PatternEquation::release(pateq);
            if (templ != (ConstructTpl*)0)
                delete templ;
            for (int4 i = 0; i < namedtempl.size(); ++i)
            {
                ConstructTpl* ntpl = namedtempl[i];
                if (ntpl != (ConstructTpl*)0)
                    delete ntpl;
            }
            vector<ContextChange*>::iterator iter;
            for (iter = context.begin(); iter != context.end(); ++iter)
                delete* iter;
        }

        public TokenPattern buildPattern(TextWriter s)
        {
            if (pattern != (TokenPattern*)0) return pattern; // Already built

            pattern = new TokenPattern();
            vector<TokenPattern> oppattern;
            bool recursion = false;
            // Generate pattern for each operand, store in oppattern
            for (int4 i = 0; i < operands.size(); ++i)
            {
                OperandSymbol* sym = operands[i];
                TripleSymbol* triple = sym->getDefiningSymbol();
                PatternExpression* defexp = sym->getDefiningExpression();
                if (triple != (TripleSymbol*)0)
                {
                    SubtableSymbol* subsym = dynamic_cast<SubtableSymbol*>(triple);
                    if (subsym != (SubtableSymbol*)0)
                    {
                        if (subsym->isBeingBuilt())
                        { // Detected recursion
                            if (recursion)
                            {
                                throw SleighError("Illegal recursion");
                            }
                            // We should also check that recursion is rightmost extreme
                            recursion = true;
                            oppattern.emplace_back();
                        }
                        else
                            oppattern.push_back(*subsym->buildPattern(s));
                    }
                    else
                        oppattern.push_back(triple->getPatternExpression()->genMinPattern(oppattern));
                }
                else if (defexp != (PatternExpression*)0)
                    oppattern.push_back(defexp->genMinPattern(oppattern));
                else
                {
                    throw SleighError(sym->getName() + ": operand is undefined");
                }
                TokenPattern & sympat(oppattern.back());
                sym->minimumlength = sympat.getMinimumLength();
                if (sympat.getLeftEllipsis() || sympat.getRightEllipsis())
                    sym->setVariableLength();
            }

            if (pateq == (PatternEquation*)0)
                throw SleighError("Missing equation");

            // Build the entire pattern
            pateq->genPattern(oppattern);
            *pattern = pateq->getTokenPattern();
            if (pattern->alwaysFalse())
                throw SleighError("Impossible pattern");
            if (recursion)
                pattern->setRightEllipsis(true);
            minimumlength = pattern->getMinimumLength(); // Get length of the pattern in bytes

            // Resolve offsets of the operands
            OperandResolve resolve(operands);
            if (!pateq->resolveOperandLeft(resolve))
                throw SleighError("Unable to resolve operand offsets");

            for (int4 i = 0; i < operands.size(); ++i)
            { // Unravel relative offsets to absolute (if possible)
                int4 base,offset;
                OperandSymbol* sym = operands[i];
                if (sym->isOffsetIrrelevant())
                {
                    sym->offsetbase = -1;
                    sym->reloffset = 0;
                    continue;
                }
                base = sym->offsetbase;
                offset = sym->reloffset;
                while (base >= 0)
                {
                    sym = operands[base];
                    if (sym->isVariableLength()) break; // Cannot resolve to absolute
                    base = sym->offsetbase;
                    offset += sym->getMinimumLength();
                    offset += sym->reloffset;
                    if (base < 0)
                    {
                        operands[i]->offsetbase = base;
                        operands[i]->reloffset = offset;
                    }
                }
            }

            // Make sure context expressions are valid
            for (int4 i = 0; i < context.size(); ++i)
                context[i]->validate();

            orderOperands();        // Order the operands based on offset dependency
            return pattern;
        }

        public TokenPattern getPattern() => pattern;

        public void setMinimumLength(int4 l)
        {
            minimumlength = l;
        }

        public int4 getMinimumLength() => minimumlength;

        public void setId(uintm i)
        {
            id = i;
        }

        public uintm getId() => id;

        public void setLineno(int4 ln)
        {
            lineno = ln;
        }

        public int4 getLineno() => lineno;

        public void setSrcIndex(int4 index)
        {
            src_index = index;
        }

        public int4 getSrcIndex() => src_index;

        public void addContext(List<ContextChange> vec)
        {
            context = vec;
        }

        public void addOperand(OperandSymbol sym)
        {
            string operstring = "\n ";  // Indicater character for operand
            operstring[1] = ('A' + operands.size()); // Encode index of operand
            operands.push_back(sym);
            printpiece.push_back(operstring); // Placeholder for operand's string
        }

        public void addInvisibleOperand(OperandSymbol sym)
        {
            operands.push_back(sym);
        }

        public void addSyntax(string syn)
        {
            string syntrim;

            if (syn.size() == 0) return;
            bool hasNonSpace = false;
            for (int4 i = 0; i < syn.size(); ++i)
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
                printpiece.push_back(syntrim);
            else if (printpiece.back() == " " && syntrim == " ")
            {
                // Don't add more whitespace
            }
            else if (printpiece.back()[0] == '\n' || printpiece.back() == " " || syntrim == " ")
                printpiece.push_back(syntrim);
            else
            {
                printpiece.back() += syntrim;
            }
        }

        public void addEquation(PatternEquation pe)
        {
            (pateq = pe)->layClaim();
        }

        public void setMainSection(ConstructTpl tpl)
        {
            templ = tpl;
        }

        public void setNamedSection(ConstructTpl tpl, int4 id)
        {               // Add a named section to the constructor
            while (namedtempl.size() <= id)
                namedtempl.push_back((ConstructTpl*)0);
            namedtempl[id] = tpl;
        }

        public SubtableSymbol getParent() => parent;

        public int4 getNumOperands() => operands.size();

        public OperandSymbol getOperand(int4 i) => operands[i];

        public PatternEquation getPatternEquation() => pateq;

        public ConstructTpl getTempl() => templ;

        public ConstructTpl getNamedTempl(int4 secnum)
        {
            if (secnum < namedtempl.size())
                return namedtempl[secnum];
            return (ConstructTpl*)0;
        }

        public int4 getNumSections() => namedtempl.size();

        public void printInfo(TextWriter s)
        {               // Print identifying information about constructor
                        // for use in error messages
            s << "table \"" << parent->getName();
            s << "\" constructor starting at line " << dec << lineno;
        }

        public void print(TextWriter s, ParserWalker pos)
        {
            vector<string>::const_iterator piter;

            for (piter = printpiece.begin(); piter != printpiece.end(); ++piter)
            {
                if ((*piter)[0] == '\n')
                {
                    int4 index = (*piter)[1] - 'A';
                    operands[index]->print(s, walker);
                }
                else
                    s << *piter;
            }
        }

        public void printMnemonic(TextWriter s, ParserWalker walker)
        {
            if (flowthruindex != -1)
            {
                SubtableSymbol* sym = dynamic_cast<SubtableSymbol*>(operands[flowthruindex]->getDefiningSymbol());
                if (sym != (SubtableSymbol*)0)
                {
                    walker.pushOperand(flowthruindex);
                    walker.getConstructor()->printMnemonic(s, walker);
                    walker.popOperand();
                    return;
                }
            }
            int4 endind = (firstwhitespace == -1) ? printpiece.size() : firstwhitespace;
            for (int4 i = 0; i < endind; ++i)
            {
                if (printpiece[i][0] == '\n')
                {
                    int4 index = printpiece[i][1] - 'A';
                    operands[index]->print(s, walker);
                }
                else
                    s << printpiece[i];
            }
        }

        public void printBody(TextWriter s, ParserWalker walker)
        {
            if (flowthruindex != -1)
            {
                SubtableSymbol* sym = dynamic_cast<SubtableSymbol*>(operands[flowthruindex]->getDefiningSymbol());
                if (sym != (SubtableSymbol*)0)
                {
                    walker.pushOperand(flowthruindex);
                    walker.getConstructor()->printBody(s, walker);
                    walker.popOperand();
                    return;
                }
            }
            if (firstwhitespace == -1) return; // Nothing to print after firstwhitespace
            for (int4 i = firstwhitespace + 1; i < printpiece.size(); ++i)
            {
                if (printpiece[i][0] == '\n')
                {
                    int4 index = printpiece[i][1] - 'A';
                    operands[index]->print(s, walker);
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
                (*iter)->apply(walker);
        }

        public void markSubtableOperands(List<int4> check)
        { // Adjust -check- so it has one entry for every operand, a 0 if it is a subtable, a 2 if it is not
            check.resize(operands.size());
            for (int4 i = 0; i < operands.size(); ++i)
            {
                TripleSymbol* sym = operands[i]->getDefiningSymbol();
                if ((sym != (TripleSymbol*)0) && (sym->getType() == SleighSymbol::subtable_symbol))
                    check[i] = 0;
                else
                    check[i] = 2;
            }
        }

        public void collectLocalExports(List<uintb> results)
        {
            if (templ == (ConstructTpl*)0) return;
            HandleTpl* handle = templ->getResult();
            if (handle == (HandleTpl*)0) return;
            if (handle->getSpace().isConstSpace()) return;  // Even if the value is dynamic, the pointed to value won't get used
            if (handle->getPtrSpace().getType() != ConstTpl::real)
            {
                if (handle->getTempSpace().isUniqueSpace())
                    results.push_back(handle->getTempOffset().getReal());
                return;
            }
            if (handle->getSpace().isUniqueSpace())
            {
                results.push_back(handle->getPtrOffset().getReal());
                return;
            }
            if (handle->getSpace().getType() == ConstTpl::handle)
            {
                int4 handleIndex = handle->getSpace().getHandleIndex();
                OperandSymbol* opSym = getOperand(handleIndex);
                opSym->collectLocalValues(results);
            }
        }

        public void setError(bool val)
        {
            inerror = val;
        }

        public bool isError() => inerror;

        public bool isRecursive()
        { // Does this constructor cause recursion with its table
            for (int4 i = 0; i < operands.size(); ++i)
            {
                TripleSymbol* sym = operands[i]->getDefiningSymbol();
                if (sym == parent) return true;
            }
            return false;
        }

        public void saveXml(TextWriter s)
        {
            s << "<constructor";
            s << " parent=\"0x" << hex << parent->getId() << "\"";
            s << " first=\"" << dec << firstwhitespace << "\"";
            s << " length=\"" << minimumlength << "\"";
            s << " line=\"" << src_index << ":" << lineno << "\">\n";
            for (int4 i = 0; i < operands.size(); ++i)
                s << "<oper id=\"0x" << hex << operands[i]->getId() << "\"/>\n";
            for (int4 i = 0; i < printpiece.size(); ++i)
            {
                if (printpiece[i][0] == '\n')
                {
                    int4 index = printpiece[i][1] - 'A';
                    s << "<opprint id=\"" << dec << index << "\"/>\n";
                }
                else
                {
                    s << "<print piece=\"";
                    xml_escape(s, printpiece[i].c_str());
                    s << "\"/>\n";
                }
            }
            for (int4 i = 0; i < context.size(); ++i)
                context[i]->saveXml(s);
            if (templ != (ConstructTpl*)0)
                templ->saveXml(s, -1);
            for (int4 i = 0; i < namedtempl.size(); ++i)
            {
                if (namedtempl[i] == (ConstructTpl*)0) // Some sections may be NULL
                    continue;
                namedtempl[i]->saveXml(s, i);
            }
            s << "</constructor>\n";
        }

        public void restoreXml(Element el, SleighBase trans)
        {
            uintm id;
            {
                istringstream s(el->getAttributeValue("parent"));
                s.unsetf(ios::dec | ios::hex | ios::oct);
                s >> id;
                parent = (SubtableSymbol*)trans->findSymbol(id);
            }
            {
                istringstream s(el->getAttributeValue("first"));
                s.unsetf(ios::dec | ios::hex | ios::oct);
                s >> firstwhitespace;
            }
            {
                istringstream s(el->getAttributeValue("length"));
                s.unsetf(ios::dec | ios::hex | ios::oct);
                s >> minimumlength;
            }
            {
                string src_and_line = el->getAttributeValue("line");
                size_t pos = src_and_line.find(":");
                src_index = stoi(src_and_line.substr(0, pos), NULL, 10);
                lineno = stoi(src_and_line.substr(pos + 1, src_and_line.length()), NULL, 10);
            }
            List list = el->getChildren();
            List::const_iterator iter;
            iter = list.begin();
            while (iter != list.end())
            {
                if ((*iter)->getName() == "oper")
                {
                    uintm id;
                    {
                        istringstream s((* iter)->getAttributeValue("id"));
                        s.unsetf(ios::dec | ios::hex | ios::oct);
                        s >> id;
                    }
                    OperandSymbol* sym = (OperandSymbol*)trans->findSymbol(id);
                    operands.push_back(sym);
                }
                else if ((*iter)->getName() == "print")
                    printpiece.push_back((*iter)->getAttributeValue("piece"));
                else if ((*iter)->getName() == "opprint")
                {
                    int4 index;
                    istringstream s((* iter)->getAttributeValue("id"));
                    s.unsetf(ios::dec | ios::hex | ios::oct);
                    s >> index;
                    string operstring = "\n ";
                    operstring[1] = ('A' + index);
                    printpiece.push_back(operstring);
                }
                else if ((*iter)->getName() == "context_op")
                {
                    ContextOp* c_op = new ContextOp();
                    c_op->restoreXml(*iter, trans);
                    context.push_back(c_op);
                }
                else if ((*iter)->getName() == "commit")
                {
                    ContextCommit* c_op = new ContextCommit();
                    c_op->restoreXml(*iter, trans);
                    context.push_back(c_op);
                }
                else
                {
                    ConstructTpl* cur = new ConstructTpl();
                    int4 sectionid = cur->restoreXml(*iter, trans);
                    if (sectionid < 0)
                    {
                        if (templ != (ConstructTpl*)0)
                            throw new LowlevelError("Duplicate main section");
                        templ = cur;
                    }
                    else
                    {
                        while (namedtempl.size() <= sectionid)
                            namedtempl.push_back((ConstructTpl*)0);
                        if (namedtempl[sectionid] != (ConstructTpl*)0)
                            throw new LowlevelError("Duplicate named section");
                        namedtempl[sectionid] = cur;
                    }
                }
                ++iter;
            }
            pattern = (TokenPattern*)0;
            if ((printpiece.size() == 1) && (printpiece[0][0] == '\n'))
                flowthruindex = printpiece[0][1] - 'A';
            else
                flowthruindex = -1;
        }
    }
}
