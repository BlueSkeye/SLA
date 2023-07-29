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
    internal class SymbolTable
    {
        private List<SleighSymbol> symbollist;
        private List<SymbolScope> table;
        private SymbolScope curscope;

        private SymbolScope skipScope(int4 i)
        {
            SymbolScope* res = curscope;
            while (i > 0)
            {
                if (res.parent == (SymbolScope*)0) return res;
                res = res.parent;
                --i;
            }
            return res;
        }

        private SleighSymbol findSymbolInternal(SymbolScope scope, string nm)
        {
            SleighSymbol* res;

            while (scope != (SymbolScope*)0)
            {
                res = scope.findSymbol(nm);
                if (res != (SleighSymbol*)0)
                    return res;
                scope = scope.getParent(); // Try higher scope
            }
            return (SleighSymbol*)0;
        }

        private void renumber()
        {               // Renumber all the scopes and symbols
                        // so that there are no gaps
            vector<SymbolScope*> newtable;
            vector<SleighSymbol*> newsymbol;
            // First renumber the scopes
            SymbolScope* scope;
            for (int4 i = 0; i < table.size(); ++i)
            {
                scope = table[i];
                if (scope != (SymbolScope*)0)
                {
                    scope.id = newtable.size();
                    newtable.push_back(scope);
                }
            }
            // Now renumber the symbols
            SleighSymbol* sym;
            for (int4 i = 0; i < symbollist.size(); ++i)
            {
                sym = symbollist[i];
                if (sym != (SleighSymbol*)0)
                {
                    sym.scopeid = table[sym.scopeid].id;
                    sym.id = newsymbol.size();
                    newsymbol.push_back(sym);
                }
            }
            table = newtable;
            symbollist = newsymbol;
        }

        public SymbolTable()
        {
            curscope = (SymbolScope*)0;
        }
        
        ~SymbolTable()
        {
            vector<SymbolScope*>::iterator iter;
            for (iter = table.begin(); iter != table.end(); ++iter)
                delete* iter;
            vector<SleighSymbol*>::iterator siter;
            for (siter = symbollist.begin(); siter != symbollist.end(); ++siter)
                delete* siter;
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
            curscope = new SymbolScope(curscope, table.size());
            table.push_back(curscope);
        }

        // Make parent of current scope current
        public void popScope()
        {
            if (curscope != (SymbolScope*)0)
                curscope = curscope.getParent();
        }

        public void addGlobalSymbol(SleighSymbol a)
        {
            a.id = symbollist.size();
            symbollist.push_back(a);
            SymbolScope* scope = getGlobalScope();
            a.scopeid = scope.getId();
            SleighSymbol* res = scope.addSymbol(a);
            if (res != a)
                throw SleighError("Duplicate symbol name '" + a.getName() + "'");
        }

        public void addSymbol(SleighSymbol a)
        {
            a.id = symbollist.size();
            symbollist.push_back(a);
            a.scopeid = curscope.getId();
            SleighSymbol* res = curscope.addSymbol(a);
            if (res != a)
                throw SleighError("Duplicate symbol name: " + a.getName());
        }

        public SleighSymbol findSymbol(string nm) => findSymbolInternal(curscope, nm);

        public SleighSymbol findSymbol(string nm, int4 skip)
            => findSymbolInternal(skipScope(skip), nm);

        public SleighSymbol findGlobalSymbol(string nm) => findSymbolInternal(table[0],nm);

        public SleighSymbol findSymbol(uintm id) => symbollist[id];

        public void replaceSymbol(SleighSymbol a, SleighSymbol b)
        {               // Replace symbol a with symbol b
                        // assuming a and b have the same name
            SleighSymbol* sym;
            int4 i = table.size() - 1;

            while (i >= 0)
            {           // Find the particular symbol
                sym = table[i].findSymbol(a.getName());
                if (sym == a)
                {
                    table[i].removeSymbol(a);
                    b.id = a.id;
                    b.scopeid = a.scopeid;
                    symbollist[b.id] = b;
                    table[i].addSymbol(b);
                    delete a;
                    return;
                }
                --i;
            }
        }

        public void saveXml(TextWriter s)
        {
            s << "<symbol_table";
            s << " scopesize=\"" << dec << table.size() << "\"";
            s << " symbolsize=\"" << symbollist.size() << "\">\n";
            for (int4 i = 0; i < table.size(); ++i)
            {
                s << "<scope id=\"0x" << hex << table[i].getId() << "\"";
                s << " parent=\"0x";
                if (table[i].getParent() == (SymbolScope*)0)
                    s << "0";
                else
                    s << hex << table[i].getParent().getId();
                s << "\"/>\n";
            }

            // First save the headers
            for (int4 i = 0; i < symbollist.size(); ++i)
                symbollist[i].saveXmlHeader(s);

            // Now save the content of each symbol
            for (int4 i = 0; i < symbollist.size(); ++i) // Must save IN ORDER
                symbollist[i].saveXml(s);
            s << "</symbol_table>\n";
        }

        public void restoreXml(Element el, SleighBase trans)
        {
            {
                uint4 size;
                istringstream s(el.getAttributeValue("scopesize"));
                s.unsetf(ios::dec | ios::hex | ios::oct);
                s >> size;
                table.resize(size, (SymbolScope*)0);
            }
            {
                uint4 size;
                istringstream s(el.getAttributeValue("symbolsize"));
                s.unsetf(ios::dec | ios::hex | ios::oct);
                s >> size;
                symbollist.resize(size, (SleighSymbol*)0);
            }
            List list = el.getChildren();
            List::const_iterator iter;
            iter = list.begin();
            for (int4 i = 0; i < table.size(); ++i)
            { // Restore the scopes
                Element* subel = *iter;
                if (subel.getName() != "scope")
                    throw SleighError("Misnumbered symbol scopes");
                uintm id;
                uintm parent;
                {
                    istringstream s = new istringstream(subel.getAttributeValue("id"));
                    s.unsetf(ios::dec | ios::hex | ios::oct);
                    s >> id;
                }
                {
                    istringstream s = new istringstream(subel.getAttributeValue("parent"));
                    s.unsetf(ios::dec | ios::hex | ios::oct);
                    s >> parent;
                }
                SymbolScope* parscope = (parent == id) ? (SymbolScope*)0 : table[parent];
                table[id] = new SymbolScope(parscope, id);
                ++iter;
            }
            curscope = table[0];        // Current scope is global

            // Now restore the symbol shells
            for (int4 i = 0; i < symbollist.size(); ++i)
            {
                restoreSymbolHeader(*iter);
                ++iter;
            }
            // Now restore the symbol content
            while (iter != list.end())
            {
                Element* subel = *iter;
                uintm id;
                SleighSymbol* sym;
                {
                    istringstream s(subel.getAttributeValue("id"));
                    s.unsetf(ios::dec | ios::hex | ios::oct);
                    s >> id;
                }
                sym = findSymbol(id);
                sym.restoreXml(subel, trans);
                ++iter;
            }
        }

        public void restoreSymbolHeader(Element el)
        {               // Put the shell of a symbol in the symbol table
                        // in order to allow recursion
            SleighSymbol* sym;
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
                throw SleighError("Bad symbol xml");
            sym.restoreXmlHeader(el);  // Restore basic elements of symbol
            symbollist[sym.id] = sym;  // Put the basic symbol in the table
            table[sym.scopeid].addSymbol(sym); // to allow recursion
        }

        public void purge()
        {               // Get rid of unsavable symbols and scopes
            SleighSymbol* sym;
            for (int4 i = 0; i < symbollist.size(); ++i)
            {
                sym = symbollist[i];
                if (sym == (SleighSymbol*)0) continue;
                if (sym.scopeid != 0)
                { // Not in global scope
                    if (sym.getType() == SleighSymbol::operand_symbol) continue;
                }
                else
                {
                    switch (sym.getType())
                    {
                        case SleighSymbol::space_symbol:
                        case SleighSymbol::token_symbol:
                        case SleighSymbol::epsilon_symbol:
                        case SleighSymbol::section_symbol:
                            break;
                        case SleighSymbol::macro_symbol:
                            {           // Delete macro's local symbols
                                MacroSymbol* macro = (MacroSymbol*)sym;
                                for (int4 i = 0; i < macro.getNumOperands(); ++i)
                                {
                                    SleighSymbol* opersym = macro.getOperand(i);
                                    table[opersym.scopeid].removeSymbol(opersym);
                                    symbollist[opersym.id] = (SleighSymbol*)0;
                                    delete opersym;
                                }
                                break;
                            }
                        case SleighSymbol::subtable_symbol:
                            {           // Delete unused subtables
                                SubtableSymbol* subsym = (SubtableSymbol*)sym;
                                if (subsym.getPattern() != (TokenPattern*)0) continue;
                                for (int4 i = 0; i < subsym.getNumConstructors(); ++i)
                                { // Go thru each constructor
                                    Constructor* con = subsym.getConstructor(i);
                                    for (int4 j = 0; j < con.getNumOperands(); ++j)
                                    { // Go thru each operand
                                        OperandSymbol* oper = con.getOperand(j);
                                        table[oper.scopeid].removeSymbol(oper);
                                        symbollist[oper.id] = (SleighSymbol*)0;
                                        delete oper;
                                    }
                                }
                                break;      // Remove the subtable symbol itself
                            }
                        default:
                            continue;
                    }
                }
                table[sym.scopeid].removeSymbol(sym); // Remove the symbol
                symbollist[i] = (SleighSymbol*)0;
                delete sym;
            }
            for (int4 i = 1; i < table.size(); ++i)
            { // Remove any empty scopes
                if (table[i].tree.empty())
                {
                    delete table[i];
                    table[i] = (SymbolScope*)0;
                }
            }
            renumber();
        }
    }
}
