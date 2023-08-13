using Sla.CORE;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Sla.SLEIGH
{
    internal class PcodeSnippet : PcodeCompile
    {
        private PcodeLexer lexer;
        private readonly SleighBase sleigh;   // Language from which we get symbols
        private SymbolTree tree;        // Symbols in the local scope of the snippet  (temporaries)
        private uint tempbase;
        private int errorcount;
        private string firsterror;
        private ConstructTpl result;

        protected virtual uint allocateTemp()
        { // Allocate a variable in the unique space and return the offset
            uint res = tempbase;
            tempbase += 16;
            return res;
        }

        protected virtual void addSymbol(SleighSymbol sym)
        {
            pair<SymbolTree::iterator, bool> res;

            res = tree.insert(sym);
            if (!res.second)
            {
                reportError((Location)null,"Duplicate symbol name: " + sym.getName());
                delete sym;     // Symbol is unattached to anything else
            }
        }

        public PcodeSnippet(SleighBase slgh)
        {
            sleigh = slgh;
            tempbase = 0;
            errorcount = 0;
            result = (ConstructTpl)null;
            setDefaultSpace(slgh.getDefaultCodeSpace());
            setConstantSpace(slgh.getConstantSpace());
            setUniqueSpace(slgh.getUniqueSpace());
            int num = slgh.numSpaces();
            for (int i = 0; i < num; ++i)
            {
                AddrSpace* spc = slgh.getSpace(i);
                spacetype type = spc.getType();
                if ((type == spacetype.IPTR_CONSTANT) || (type == spacetype.IPTR_PROCESSOR) || (type == spacetype.IPTR_SPACEBASE) || (type == spacetype.IPTR_INTERNAL))
                    tree.insert(new SpaceSymbol(spc));
            }
            addSymbol(new FlowDestSymbol("inst_dest", slgh.getConstantSpace()));
            addSymbol(new FlowRefSymbol("inst_ref", slgh.getConstantSpace()));
        }

        public void setResult(ConstructTpl res)
        {
            result = res;
        }

        public ConstructTpl releaseResult()
        {
            ConstructTpl res = result;
            result = (ConstructTpl)null;
            return res;
        }
        
        ~PcodeSnippet()
        {
            SymbolTree::iterator iter;
            for (iter = tree.begin(); iter != tree.end(); ++iter)
                delete* iter;       // Free ALL temporary symbols
            if (result != (ConstructTpl)null)
            {
                delete result;
                result = (ConstructTpl)null;
            }
        }

        public virtual Location getLocation(SleighSymbol sym) => (Location)null;

        public virtual void reportError(Location loc, string msg)
        {
            if (errorcount == 0)
                firsterror = msg;
            errorcount += 1;
        }

        public virtual void reportWarning(Location loc, string msg)
        {
        }

        public bool hasErrors() => (errorcount != 0);

        public string getErrorMessage() => firsterror;

        public void setUniqueBase(uint val)
        {
            tempbase = val;
        }

        public uint getUniqueBase() => tempbase;

        public void clear()
        { // Clear everything, prepare for a new parse against the same language
            SymbolTree::iterator iter, tmpiter;
            iter = tree.begin();
            while (iter != tree.end())
            {
                SleighSymbol* sym = *iter;
                tmpiter = iter;
                ++iter;         // Increment now, as node may be deleted
                if (sym.getType() != SleighSymbol::space_symbol)
                {
                    delete sym;     // Free any old local symbols
                    tree.erase(tmpiter);
                }
            }
            if (result != (ConstructTpl)null)
            {
                delete result;
                result = (ConstructTpl)null;
            }
            // tempbase = 0;
            errorcount = 0;
            firsterror.clear();
            resetLabelCount();
        }

        public int lex()
        {
            int tok = lexer.getNextToken();
            if (tok == STRING)
            {
                SleighSymbol* sym;
                SleighSymbol tmpsym(lexer.getIdentifier());
                SymbolTree::const_iterator iter = tree.find(&tmpsym);
                if (iter != tree.end())
                    sym = *iter;        // Found a local symbol
                else
                    sym = sleigh.findSymbol(lexer.getIdentifier());
                if (sym != (SleighSymbol)null)
                {
                    switch (sym.getType())
                    {
                        case SleighSymbol::space_symbol:
                            yylval.spacesym = (SpaceSymbol*)sym;
                            return SPACESYM;
                        case SleighSymbol::userop_symbol:
                            yylval.useropsym = (UserOpSymbol*)sym;
                            return USEROPSYM;
                        case SleighSymbol::varnode_symbol:
                            yylval.varsym = (VarnodeSymbol*)sym;
                            return VARSYM;
                        case SleighSymbol::operand_symbol:
                            yylval.operandsym = (OperandSymbol*)sym;
                            return OPERANDSYM;
                        case SleighSymbol::start_symbol:
                            yylval.startsym = (StartSymbol*)sym;
                            return STARTSYM;
                        case SleighSymbol::end_symbol:
                            yylval.endsym = (EndSymbol*)sym;
                            return ENDSYM;
                        case SleighSymbol::next2_symbol:
                            yylval.next2sym = (Next2Symbol*)sym;
                            return NEXT2SYM;
                        case SleighSymbol::label_symbol:
                            yylval.labelsym = (LabelSymbol*)sym;
                            return LABELSYM;
                        case SleighSymbol::dummy_symbol:
                            break;
                        default:
                            // The translator may have other symbols in it that we don't want visible in the snippet compiler
                            break;
                    }
                }
                yylval.str = new string(lexer.getIdentifier());
                return STRING;
            }
            if (tok == INTEGER)
            {
                yylval.i = new ulong(lexer.getNumber());
                return INTEGER;
            }
            return tok;
        }

        public bool parseStream(istream s)
        {
            lexer.initialize(&s);
            pcode = this;           // Setup global object for yyparse
            int res = yyparse();
            if (res != 0)
            {
                reportError((Location)null,"Syntax error");
                return false;
            }
            if (!PcodeCompile::propagateSize(result))
            {
                reportError((Location)null,"Could not resolve at least 1 variable size");
                return false;
            }
            return true;
        }

        public void addOperand(string name, int index)
        { // Add an operand symbol for this snippet
            OperandSymbol* sym = new OperandSymbol(name, index, (Constructor)null);
            addSymbol(sym);
        }
    }
}
