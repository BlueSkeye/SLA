using Sla.CORE;

namespace Sla.SLEIGH
{
    internal class Constructor
    {
        // This is NOT a symbol
        private TokenPattern pattern;
        private SubtableSymbol parent;
        private PatternEquation pateq;
        private List<OperandSymbol> operands = new List<OperandSymbol>();
        private List<string> printpiece = new List<string>();
        private List<ContextChange> context = new List<ContextChange>(); // Context commands
        private ConstructTpl templ;        // The main p-code section
        private List<ConstructTpl> namedtempl = new List<ConstructTpl>(); // Other named p-code sections
        private int minimumlength;     // Minimum length taken up by this constructor in bytes
        private uint id;           // Unique id of constructor within subtable
        private int firstwhitespace;       // Index of first whitespace piece in -printpiece-
        private int flowthruindex;     // if >=0 then print only a single operand no markup
        private int lineno;
        private int src_index;           //source file index
        private /*mutable*/ bool inerror;                 // An error is associated with this Constructor

        private void orderOperands()
        {
            OperandSymbol sym;
            List<OperandSymbol> patternorder = new List<OperandSymbol>();
            List<OperandSymbol> newops = new List<OperandSymbol>(); // New order of the operands
            int lastsize;

            pateq.operandOrder(this, patternorder);
            for (int i = 0; i < operands.size(); ++i) {
                // Make sure patternorder contains all operands
                sym = operands[i];
                if (!sym.isMarked()) {
                    patternorder.Add(sym);
                    sym.setMark();     // Make sure all operands are marked
                }
            }
            do {
                lastsize = newops.size();
                for (int i = 0; i < patternorder.size(); ++i) {
                    sym = patternorder[i];
                    if (!sym.isMarked()) continue; // "unmarked" means it is already in newops
                    if (sym.isOffsetIrrelevant()) continue; // expression Operands come last
                    if ((sym.offsetbase == -1) || (!operands[(int)sym.offsetbase].isMarked())) {
                        newops.Add(sym);
                        sym.clearMark();
                    }
                }
            } while (newops.size() != lastsize);
            for (int i = 0; i < patternorder.size(); ++i) {
                // Tack on expression Operands
                sym = patternorder[i];
                if (sym.isOffsetIrrelevant()) {
                    newops.Add(sym);
                    sym.clearMark();
                }
            }

            if (newops.size() != operands.size())
                throw new SleighError("Circular offset dependency between operands");

            for (int i = 0; i < newops.size(); ++i) {
                // Fix up operand indices
                newops[i].hand = i;
                newops[i].localexp.changeIndex(i);
            }
            List<int> handmap = new List<int>();       // Create index translation map
            for (int i = 0; i < operands.size(); ++i)
                handmap.Add(operands[i].hand);

            // Fix up offsetbase
            for (int i = 0; i < newops.size(); ++i) {
                sym = newops[i];
                if (sym.offsetbase == -1) continue;
                sym.offsetbase = handmap[sym.offsetbase];
            }

            if (templ != (ConstructTpl)null) // Fix up templates
                templ.changeHandleIndex(handmap);
            for (int i = 0; i < namedtempl.size(); ++i) {
                ConstructTpl? ntempl = namedtempl[i];
                if (ntempl != (ConstructTpl)null)
                    ntempl.changeHandleIndex(handmap);
            }

            // Fix up printpiece operand refs
            for (int i = 0; i < printpiece.size(); ++i) {
                if (printpiece[i][0] == '\n') {
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
            parent = (SubtableSymbol)null;
            pateq = (PatternEquation)null;
            templ = (ConstructTpl)null;
            firstwhitespace = -1;
            flowthruindex = -1;
            inerror = false;
        }

        public Constructor(SubtableSymbol p)
        {
            pattern = (TokenPattern)null;
            parent = p;
            pateq = (PatternEquation)null;
            templ = (ConstructTpl)null;
            firstwhitespace = -1;
            inerror = false;
        }

        ~Constructor()
        {
            //if (pattern != (TokenPattern)null)
            //    delete pattern;
            //if (pateq != (PatternEquation)null)
            //    PatternEquation.release(pateq);
            //if (templ != (ConstructTpl)null)
            //    delete templ;
            //for (int i = 0; i < namedtempl.Count; ++i) {
            //    ConstructTpl? ntpl = namedtempl[i];
            //    if (ntpl != (ConstructTpl)null)
            //        delete ntpl;
            //}
            //foreach (ContextChange change in context) {
            //    // delete deleted;
            //}
        }

        public TokenPattern buildPattern(TextWriter s)
        {
            if (pattern != (TokenPattern)null) return pattern; // Already built

            pattern = new TokenPattern();
            List<TokenPattern> oppattern = new List<TokenPattern>();
            bool recursion = false;
            // Generate pattern for each operand, store in oppattern
            for (int i = 0; i < operands.Count; ++i) {
                OperandSymbol sym = operands[i];
                TripleSymbol? triple = sym.getDefiningSymbol();
                PatternExpression defexp = sym.getDefiningExpression();
                if (triple != (TripleSymbol)null) {
                    SubtableSymbol? subsym = triple as SubtableSymbol;
                    if (subsym != (SubtableSymbol)null) {
                        if (subsym.isBeingBuilt()) {
                            // Detected recursion
                            if (recursion) {
                                throw new SleighError("Illegal recursion");
                            }
                            // We should also check that recursion is rightmost extreme
                            recursion = true;
                            oppattern.Add(new TokenPattern());
                        }
                        else
                            oppattern.Add(subsym.buildPattern(s));
                    }
                    else
                        oppattern.Add(triple.getPatternExpression().genMinPattern(oppattern));
                }
                else if (defexp != (PatternExpression)null)
                    oppattern.Add(defexp.genMinPattern(oppattern));
                else {
                    throw new SleighError(sym.getName() + ": operand is undefined");
                }
                TokenPattern sympat = oppattern.GetLastItem();
                sym.minimumlength = sympat.getMinimumLength();
                if (sympat.getLeftEllipsis() || sympat.getRightEllipsis())
                    sym.setVariableLength();
            }

            if (pateq == (PatternEquation)null)
                throw new SleighError("Missing equation");

            // Build the entire pattern
            pateq.genPattern(oppattern);
            pattern = pateq.getTokenPattern();
            if (pattern.alwaysFalse())
                throw new SleighError("Impossible pattern");
            if (recursion)
                pattern.setRightEllipsis(true);
            minimumlength = pattern.getMinimumLength(); // Get length of the pattern in bytes

            // Resolve offsets of the operands
            OperandResolve resolve = new OperandResolve(operands);
            if (!pateq.resolveOperandLeft(resolve))
                throw new SleighError("Unable to resolve operand offsets");

            for (int i = 0; i < operands.size(); ++i) {
                // Unravel relative offsets to absolute (if possible)
                int @base;
                int offset;
                OperandSymbol sym = operands[i];
                if (sym.isOffsetIrrelevant()) {
                    sym.offsetbase = -1;
                    sym.reloffset = 0;
                    continue;
                }
                @base = sym.offsetbase;
                offset = (int)sym.reloffset;
                while (@base >= 0) {
                    sym = operands[@base];
                    if (sym.isVariableLength()) break; // Cannot resolve to absolute
                    @base = sym.offsetbase;
                    offset += sym.getMinimumLength();
                    offset += (int)sym.reloffset;
                    if (@base < 0) {
                        operands[i].offsetbase = @base;
                        operands[i].reloffset = (uint)offset;
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

            if (syn.Length == 0) return;
            bool hasNonSpace = false;
            for (int i = 0; i < syn.Length; ++i) {
                if (syn[i] != ' ') {
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
            else if (printpiece.GetLastItem() == " " && syntrim == " ") {
                // Don't add more whitespace
            }
            else if (printpiece.GetLastItem()[0] == '\n' || printpiece.GetLastItem() == " " || syntrim == " ")
                printpiece.Add(syntrim);
            else {
                printpiece[printpiece.Count - 1] = printpiece.GetLastItem() + syntrim;
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
            s.Write($"table \"{parent.getName()}\" constructor starting at line {lineno}");
        }

        public void print(TextWriter s, ParserWalker pos)
        {
            foreach (string piece in printpiece) {
                if (piece[0] == '\n') {
                    int index = (*piter)[1] - 'A';
                    operands[index].print(s, walker);
                }
                else
                    s.Write(piece);
            }
        }

        public void printMnemonic(TextWriter s, ParserWalker walker)
        {
            if (flowthruindex != -1)
            {
                SubtableSymbol? sym = (operands[flowthruindex].getDefiningSymbol()) as SubtableSymbol;
                if (sym != (SubtableSymbol)null) {
                    walker.pushOperand(flowthruindex);
                    walker.getConstructor().printMnemonic(s, walker);
                    walker.popOperand();
                    return;
                }
            }
            int endind = (firstwhitespace == -1) ? printpiece.size() : firstwhitespace;
            for (int i = 0; i < endind; ++i) {
                if (printpiece[i][0] == '\n') {
                    int index = printpiece[i][1] - 'A';
                    operands[index].print(s, walker);
                }
                else
                    s.Write(printpiece[i]);
            }
        }

        public void printBody(TextWriter s, ParserWalker walker)
        {
            if (flowthruindex != -1) {
                SubtableSymbol? sym = (operands[flowthruindex].getDefiningSymbol()) as SubtableSymbol;
                if (sym != (SubtableSymbol)null) {
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
                    s.Write(printpiece[i]);
            }
        }

        public void removeTrailingSpace()
        {
            // Allow for user to force extra space at end of printing
            if ((!printpiece.empty()) && (printpiece.GetLastItem() == " "))
                printpiece.RemoveLastItem();
            //  while((!printpiece.empty())&&(printpiece.GetLastItem()==" "))
            //    printpiece.RemoveLastItem();
        }

        public void applyContext(ParserWalkerChange walker)
        {
            foreach (ContextChange change in context)
                change.apply(walker);
        }

        public void markSubtableOperands(List<int> check)
        { // Adjust -check- so it has one entry for every operand, a 0 if it is a subtable, a 2 if it is not
            check.resize(operands.size());
            for (int i = 0; i < operands.size(); ++i) {
                TripleSymbol? sym = operands[i].getDefiningSymbol();
                if ((sym != (TripleSymbol)null) && (sym.getType() ==  SleighSymbol.symbol_type.subtable_symbol))
                    check[i] = 0;
                else
                    check[i] = 2;
            }
        }

        public void collectLocalExports(List<ulong> results)
        {
            if (templ == (ConstructTpl)null) return;
            HandleTpl handle = templ.getResult();
            if (handle == (HandleTpl)null) return;
            if (handle.getSpace().isConstSpace()) return;  // Even if the value is dynamic, the pointed to value won't get used
            if (handle.getPtrSpace().getType() != ConstTpl.const_type.real)
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
            if (handle.getSpace().getType() == ConstTpl.const_type.handle)
            {
                int handleIndex = handle.getSpace().getHandleIndex();
                OperandSymbol opSym = getOperand(handleIndex);
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
                TripleSymbol sym = operands[i].getDefiningSymbol();
                if (sym == parent) return true;
            }
            return false;
        }

        public void saveXml(TextWriter s)
        {
            s.Write("<constructor");
            s.Write($" parent=\"0x{parent.getId():X}\"");
            s.Write($" first=\"{firstwhitespace}\"");
            s.Write($" length=\"{minimumlength}\"");
            s.WriteLine(" line=\"{src_index}:{lineno}\">");
            for (int i = 0; i < operands.size(); ++i)
                s.WriteLine($"<oper id=\"0x{operands[i].getId():X}\"/>");
            for (int i = 0; i < printpiece.size(); ++i) {
                if (printpiece[i][0] == '\n') {
                    int index = printpiece[i][1] - 'A';
                    s.WriteLine($"<opprint id=\"{index}\"/>");
                }
                else {
                    s.Write("<print piece=\"");
                    Xml.xml_escape(s, printpiece[i].c_str());
                    s.WriteLine("\"/>");
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
            s.WriteLine("</constructor>");
        }

        public void restoreXml(Element el, SleighBase trans)
        {
            uint id = uint.Parse(el.getAttributeValue("parent"));
            parent = (SubtableSymbol)trans.findSymbol(id);
            firstwhitespace = int.Parse(el.getAttributeValue("first"));
            minimumlength = int.Parse(el.getAttributeValue("length"));
            string src_and_line = el.getAttributeValue("line");
            int pos = src_and_line.IndexOf(":");
            src_index = stoi(src_and_line.Substring(0, pos), NULL, 10);
            lineno = stoi(src_and_line.Substring(pos + 1, src_and_line.length()), NULL, 10);
            IEnumerator<Element> iter = el.getChildren().begin();
            while (iter.MoveNext()) {
                if (iter.Current.getName() == "oper") {
                    uint id = uint.Parse(iter.Current.getAttributeValue("id"));
                    OperandSymbol sym = (OperandSymbol)trans.findSymbol(id);
                    operands.Add(sym);
                }
                else if (iter.Current.getName() == "print")
                    printpiece.Add(iter.Current.getAttributeValue("piece"));
                else if (iter.Current.getName() == "opprint") {
                    int index = int.Parse(iter.Current.getAttributeValue("id"));
                    string operstring = "\n ";
                    operstring[1] = ('A' + index);
                    printpiece.Add(operstring);
                }
                else if (iter.Current.getName() == "context_op") {
                    ContextOp c_op = new ContextOp();
                    c_op.restoreXml(iter.Current, trans);
                    context.Add(c_op);
                }
                else if (iter.Current.getName() == "commit") {
                    ContextCommit c_op = new ContextCommit();
                    c_op.restoreXml(iter.Current, trans);
                    context.Add(c_op);
                }
                else {
                    ConstructTpl cur = new ConstructTpl();
                    int sectionid = cur.restoreXml(iter.Current, trans);
                    if (sectionid < 0) {
                        if (templ != (ConstructTpl)null)
                            throw new LowlevelError("Duplicate main section");
                        templ = cur;
                    }
                    else {
                        while (namedtempl.size() <= sectionid)
                            namedtempl.Add((ConstructTpl)null);
                        if (namedtempl[sectionid] != (ConstructTpl)null)
                            throw new LowlevelError("Duplicate named section");
                        namedtempl[sectionid] = cur;
                    }
                }
            }
            pattern = (TokenPattern)null;
            if ((printpiece.size() == 1) && (printpiece[0][0] == '\n'))
                flowthruindex = printpiece[0][1] - 'A';
            else
                flowthruindex = -1;
        }
    }
}
