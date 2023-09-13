using Sla.CORE;

namespace Sla.SLEIGH
{
    internal class SymbolTable
    {
        private List<SleighSymbol> symbollist;
        private List<SymbolScope> table;
        private SymbolScope curscope;

        private SymbolScope skipScope(int i)
        {
            SymbolScope res = curscope;
            while (i > 0) {
                if (res.parent == (SymbolScope)null) return res;
                res = res.parent;
                --i;
            }
            return res;
        }

        private SleighSymbol findSymbolInternal(SymbolScope scope, string nm)
        {
            while (scope != (SymbolScope)null) {
                SleighSymbol? res = scope.findSymbol(nm);
                if (res != (SleighSymbol)null)
                    return res;
                scope = scope.getParent(); // Try higher scope
            }
            return (SleighSymbol)null;
        }

        private void renumber()
        {
            // Renumber all the scopes and symbols
            // so that there are no gaps
            List<SymbolScope> newtable = new List<SymbolScope>();
            List<SleighSymbol> newsymbol = new List<SleighSymbol>();
            // First renumber the scopes
            for (int i = 0; i < table.size(); ++i) {
                SymbolScope? scope = table[i];
                if (scope != (SymbolScope)null) {
                    scope.id = (uint)newtable.size();
                    newtable.Add(scope);
                }
            }
            // Now renumber the symbols
            for (int i = 0; i < symbollist.size(); ++i) {
                SleighSymbol? sym = symbollist[i];
                if (sym != (SleighSymbol)null) {
                    sym.scopeid = table[(int)sym.scopeid].id;
                    sym.id = (uint)newsymbol.size();
                    newsymbol.Add(sym);
                }
            }
            table = newtable;
            symbollist = newsymbol;
        }

        public SymbolTable()
        {
            curscope = (SymbolScope)null;
        }
        
        ~SymbolTable()
        {
            //List<SymbolScope*>::iterator iter;
            //for (iter = table.begin(); iter != table.end(); ++iter)
            //    delete* iter;
            //List<SleighSymbol*>::iterator siter;
            //for (siter = symbollist.begin(); siter != symbollist.end(); ++siter)
            //    delete* siter;
        }

        public SymbolScope getCurrentScope() => curscope;

        public SymbolScope getGlobalScope() => table[0];

        public void setCurrentScope(SymbolScope scope)
        {
            curscope = scope;
        }

        // Add new scope off of current scope, make it current
        public void addScope()
        {
            curscope = new SymbolScope(curscope, (uint)table.size());
            table.Add(curscope);
        }

        // Make parent of current scope current
        public void popScope()
        {
            if (curscope != (SymbolScope)null)
                curscope = curscope.getParent();
        }

        public void addGlobalSymbol(SleighSymbol a)
        {
            a.id = (uint)symbollist.size();
            symbollist.Add(a);
            SymbolScope scope = getGlobalScope();
            a.scopeid = scope.getId();
            SleighSymbol res = scope.addSymbol(a);
            if (res != a)
                throw new SleighError($"Duplicate symbol name '{a.getName()}'");
        }

        public void addSymbol(SleighSymbol a)
        {
            a.id = (uint)symbollist.size();
            symbollist.Add(a);
            a.scopeid = curscope.getId();
            SleighSymbol res = curscope.addSymbol(a);
            if (res != a)
                throw new SleighError("Duplicate symbol name: " + a.getName());
        }

        public SleighSymbol findSymbol(string nm) => findSymbolInternal(curscope, nm);

        public SleighSymbol findSymbol(string nm, int skip)
            => findSymbolInternal(skipScope(skip), nm);

        public SleighSymbol findGlobalSymbol(string nm) => findSymbolInternal(table[0],nm);

        public SleighSymbol findSymbol(uint id) => symbollist[(int)id];

        public void replaceSymbol(SleighSymbol a, SleighSymbol b)
        {
            // Replace symbol a with symbol b
            // assuming a and b have the same name
            int i = table.size() - 1;

            while (i >= 0) {
                // Find the particular symbol
                SleighSymbol? sym = table[i].findSymbol(a.getName());
                if (sym == a) {
                    table[i].removeSymbol(a);
                    b.id = a.id;
                    b.scopeid = a.scopeid;
                    symbollist[(int)b.id] = b;
                    table[i].addSymbol(b);
                    // delete a;
                    return;
                }
                --i;
            }
        }

        public void saveXml(TextWriter s)
        {
            s.Write("<symbol_table");
            s.Write($" scopesize=\"{table.size()}\"");
            s.WriteLine($" symbolsize=\"{symbollist.size()}\">");
            for (int i = 0; i < table.size(); ++i) {
                s.Write($"<scope id=\"0x{table[i].getId():X}\"");
                s.Write(" parent=\"0x");
                if (table[i].getParent() == (SymbolScope)null)
                    s.Write("0");
                else
                    s.Write($"{table[i].getParent().getId():X}");
                s.WriteLine("\"/>");
            }

            // First save the headers
            for (int i = 0; i < symbollist.size(); ++i)
                symbollist[i].saveXmlHeader(s);

            // Now save the content of each symbol
            for (int i = 0; i < symbollist.size(); ++i) // Must save IN ORDER
                symbollist[i].saveXml(s);
            s.WriteLine("</symbol_table>");
        }

        public void restoreXml(Element el, SleighBase trans)
        {
            uint size = uint.Parse(el.getAttributeValue("scopesize"));
            table.resize((int)size, (SymbolScope)null);
            size = uint.Parse(el.getAttributeValue("symbolsize"));
            symbollist.resize((int)size, (SleighSymbol)null);
            IEnumerator<Element> iter = el.getChildren().GetEnumerator();
            for (int i = 0; i < table.size(); ++i) {
                if (!iter.MoveNext()) throw new ApplicationException();
                // Restore the scopes
                Element subel = iter.Current;
                if (subel.getName() != "scope")
                    throw new SleighError("Misnumbered symbol scopes");
                uint id = uint.Parse(subel.getAttributeValue("id"));
                uint parent = uint.Parse(subel.getAttributeValue("parent"));
                SymbolScope? parscope = (parent == id) ? (SymbolScope)null : table[(int)parent];
                table[(int)id] = new SymbolScope(parscope, id);
            }
            // Current scope is global
            curscope = table[0];

            // Now restore the symbol shells
            for (int i = 0; i < symbollist.size(); ++i) {
                if (!iter.MoveNext()) throw new ApplicationException();
                restoreSymbolHeader(iter.Current);
            }
            // Now restore the symbol content
            while (iter.MoveNext()) {
                Element subel = iter.Current;
                SleighSymbol sym;
                uint id = uint.Parse(subel.getAttributeValue("id"));
                sym = findSymbol(id);
                sym.restoreXml(subel, trans);
            }
        }

        public void restoreSymbolHeader(Element el)
        {
            // Put the shell of a symbol in the symbol table
            // in order to allow recursion
            SleighSymbol sym;
            if (el.getName() == "userop_head")
                sym = new UserOpSymbol();
            else if (el.getName() == "epsilon_sym_head")
                sym = new EpsilonSymbol();
            else if (el.getName() == "value_sym_head")
                sym = new ValueSymbol();
            else if (el.getName() == "valuemap_sym_head")
                sym = new ValueMapSymbol();
            else if (el.getName() == "name_sym_head")
                sym = new NameSymbol();
            else if (el.getName() == "varnode_sym_head")
                sym = new VarnodeSymbol();
            else if (el.getName() == "context_sym_head")
                sym = new ContextSymbol();
            else if (el.getName() == "varlist_sym_head")
                sym = new VarnodeListSymbol();
            else if (el.getName() == "operand_sym_head")
                sym = new OperandSymbol();
            else if (el.getName() == "start_sym_head")
                sym = new StartSymbol();
            else if (el.getName() == "end_sym_head")
                sym = new EndSymbol();
            else if (el.getName() == "next2_sym_head")
                sym = new Next2Symbol();
            else if (el.getName() == "subtable_sym_head")
                sym = new SubtableSymbol();
            else if (el.getName() == "flowdest_sym_head")
                sym = new FlowDestSymbol();
            else if (el.getName() == "flowref_sym_head")
                sym = new FlowRefSymbol();
            else
                throw new SleighError("Bad symbol xml");
            sym.restoreXmlHeader(el);  // Restore basic elements of symbol
            symbollist[(int)sym.id] = sym;  // Put the basic symbol in the table
            table[(int)sym.scopeid].addSymbol(sym); // to allow recursion
        }

        public void purge()
        {
            // Get rid of unsavable symbols and scopes
            SleighSymbol sym;
            for (int i = 0; i < symbollist.Count; ++i) {
                sym = symbollist[i];
                if (sym == (SleighSymbol)null) continue;
                if (sym.scopeid != 0) {
                    // Not in global scope
                    if (sym.getType() == SleighSymbol.symbol_type.operand_symbol) continue;
                }
                else {
                    switch (sym.getType()) {
                        case SleighSymbol.symbol_type.space_symbol:
                        case SleighSymbol.symbol_type.token_symbol:
                        case SleighSymbol.symbol_type.epsilon_symbol:
                        case SleighSymbol.symbol_type.section_symbol:
                            break;
                        case SleighSymbol.symbol_type.macro_symbol:
                            {           // Delete macro's local symbols
                                MacroSymbol macro = (MacroSymbol)sym;
                                for (int j = 0; j < macro.getNumOperands(); ++j) {
                                    SleighSymbol opersym = macro.getOperand(j);
                                    table[(int)opersym.scopeid].removeSymbol(opersym);
                                    symbollist[(int)opersym.id] = (SleighSymbol)null;
                                    // delete opersym;
                                }
                                break;
                            }
                        case SleighSymbol.symbol_type.subtable_symbol: {
                                // Delete unused subtables
                                SubtableSymbol subsym = (SubtableSymbol)sym;
                                if (subsym.getPattern() != (TokenPattern)null) continue;
                                for (int k = 0; k < subsym.getNumConstructors(); ++k) {
                                    // Go thru each constructor
                                    Constructor con = subsym.getConstructor(k);
                                    for (int j = 0; j < con.getNumOperands(); ++j) {
                                        // Go thru each operand
                                        OperandSymbol oper = con.getOperand(j);
                                        table[(int)oper.scopeid].removeSymbol(oper);
                                        symbollist[(int)oper.id] = (SleighSymbol)null;
                                        // delete oper;
                                    }
                                }
                                break;      // Remove the subtable symbol itself
                            }
                        default:
                            continue;
                    }
                }
                table[(int)sym.scopeid].removeSymbol(sym); // Remove the symbol
                symbollist[i] = (SleighSymbol)null;
                // delete sym;
            }
            for (int i = 1; i < table.Count; ++i) {
                // Remove any empty scopes
                if (0 == table[i].tree.Count) {
                    // delete table[i];
                    table[i] = (SymbolScope)null;
                }
            }
            renumber();
        }
    }
}
