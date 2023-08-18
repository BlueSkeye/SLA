using Sla.CORE;
using Sla.SLACOMP;

using SymbolTree = System.Collections.Generic.HashSet<Sla.SLEIGH.SleighSymbol>; // SymbolCompare

namespace Sla.SLEIGH
{
    internal class PcodeSnippet : PcodeCompile
    {
        private PcodeLexer lexer;
        // Language from which we get symbols
        private readonly SleighBase sleigh;
        // Symbols in the local scope of the snippet  (temporaries)
        private SymbolTree tree;
        private uint tempbase;
        private int errorcount;
        private string firsterror;
        private ConstructTpl? result;

        protected override uint allocateTemp()
        {
            // Allocate a variable in the unique space and return the offset
            uint res = tempbase;
            tempbase += 16;
            return res;
        }

        protected override void addSymbol(SleighSymbol sym)
        {
            if (tree.Contains(sym)){
                reportError((Location)null, $"Duplicate symbol name: {sym.getName()}");
                // delete sym;     // Symbol is unattached to anything else
            }
            else {
                tree.Add(sym);
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
            for (int i = 0; i < num; ++i) {
                AddrSpace spc = slgh.getSpace(i);
                spacetype type = spc.getType();
                if (   (type == spacetype.IPTR_CONSTANT)
                    || (type == spacetype.IPTR_PROCESSOR)
                    || (type == spacetype.IPTR_SPACEBASE)
                    || (type == spacetype.IPTR_INTERNAL))
                {
                    tree.Add(new SpaceSymbol(spc));
                }
            }
            addSymbol(new FlowDestSymbol("inst_dest", slgh.getConstantSpace()));
            addSymbol(new FlowRefSymbol("inst_ref", slgh.getConstantSpace()));
        }

        public void setResult(ConstructTpl res)
        {
            result = res;
        }

        public ConstructTpl? releaseResult()
        {
            ConstructTpl? res = result;
            result = (ConstructTpl)null;
            return res;
        }
        
        ~PcodeSnippet()
        {
            //SymbolTree::iterator iter;
            //for (iter = tree.begin(); iter != tree.end(); ++iter)
            //    delete* iter;       // Free ALL temporary symbols
            if (result != (ConstructTpl)null) {
                // delete result;
                result = (ConstructTpl)null;
            }
        }

        public override Location getLocation(SleighSymbol sym) => (Location)null;

        public override void reportError(Location loc, string msg)
        {
            if (errorcount == 0)
                firsterror = msg;
            errorcount += 1;
        }

        public override void reportWarning(Location loc, string msg)
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
        {
            // Clear everything, prepare for a new parse against the same language
            IEnumerator<SleighSymbol> iter = tree.GetEnumerator();
            List<SleighSymbol> removable = new List<SleighSymbol>();
            while (iter.MoveNext()) {
                SleighSymbol sym = iter.Current;
                // Increment now, as node may be deleted
                if (sym.getType() != SleighSymbol.symbol_type.space_symbol) {
                    // delete sym;     // Free any old local symbols
                    removable.Add(sym);
                }
            }
            foreach(SleighSymbol removed in removable) {
                tree.Remove(removed);
            }
            if (result != (ConstructTpl)null) {
                // delete result;
                result = (ConstructTpl)null;
            }
            // tempbase = 0;
            errorcount = 0;
            firsterror = string.Empty;
            resetLabelCount();
        }

        public int lex()
        {
            int tok = lexer.getNextToken();
            if (tok == (int)sleightokentype.STRING) {
                SleighSymbol? sym;
                SleighSymbol tmpsym = new SleighSymbol(lexer.getIdentifier());
                if (!tree.TryGetValue(tmpsym, out sym)) {
                    sym = sleigh.findSymbol(lexer.getIdentifier());
                }
                if (sym != (SleighSymbol)null) {
                    switch (sym.getType()) {
                        case SleighSymbol.symbol_type.space_symbol:
                            yylval.spacesym = (SpaceSymbol)sym;
                            return SPACESYM;
                        case SleighSymbol.symbol_type.userop_symbol:
                            yylval.useropsym = (UserOpSymbol)sym;
                            return USEROPSYM;
                        case SleighSymbol.symbol_type.varnode_symbol:
                            yylval.varsym = (VarnodeSymbol)sym;
                            return VARSYM;
                        case SleighSymbol.symbol_type.operand_symbol:
                            yylval.operandsym = (OperandSymbol)sym;
                            return OPERANDSYM;
                        case SleighSymbol.symbol_type.start_symbol:
                            yylval.startsym = (StartSymbol)sym;
                            return STARTSYM;
                        case SleighSymbol.symbol_type.end_symbol:
                            yylval.endsym = (EndSymbol)sym;
                            return ENDSYM;
                        case SleighSymbol.symbol_type.next2_symbol:
                            yylval.next2sym = (Next2Symbol)sym;
                            return NEXT2SYM;
                        case SleighSymbol.symbol_type.label_symbol:
                            yylval.labelsym = (LabelSymbol)sym;
                            return LABELSYM;
                        case SleighSymbol.symbol_type.dummy_symbol:
                            break;
                        default:
                            // The translator may have other symbols in it that we don't want visible in the snippet compiler
                            break;
                    }
                }
                yylval.str = lexer.getIdentifier();
                return STRING;
            }
            if (tok == INTEGER) {
                yylval.i = lexer.getNumber();
                return INTEGER;
            }
            return tok;
        }

        public bool parseStream(TextReader s)
        {
            lexer.initialize(s);
            pcode = this;           // Setup global object for yyparse
            int res = yyparse();
            if (res != 0) {
                reportError((Location)null,"Syntax error");
                return false;
            }
            if (!PcodeCompile.propagateSize(result)) {
                reportError((Location)null,"Could not resolve at least 1 variable size");
                return false;
            }
            return true;
        }

        public void addOperand(string name, int index)
        {
            // Add an operand symbol for this snippet
            OperandSymbol sym = new OperandSymbol(name, index, (Constructor)null);
            addSymbol(sym);
        }
    }
}
