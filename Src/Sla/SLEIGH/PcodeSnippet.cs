﻿using Sla.CORE;
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
        private uint4 tempbase;
        private int4 errorcount;
        private string firsterror;
        private ConstructTpl result;

        protected virtual uint4 allocateTemp()
        { // Allocate a variable in the unique space and return the offset
            uint4 res = tempbase;
            tempbase += 16;
            return res;
        }

        protected virtual void addSymbol(SleighSymbol sym)
        {
            pair<SymbolTree::iterator, bool> res;

            res = tree.insert(sym);
            if (!res.second)
            {
                reportError((Location*)0,"Duplicate symbol name: " + sym.getName());
                delete sym;     // Symbol is unattached to anything else
            }
        }

        public PcodeSnippet(SleighBase slgh)
        {
            sleigh = slgh;
            tempbase = 0;
            errorcount = 0;
            result = (ConstructTpl*)0;
            setDefaultSpace(slgh.getDefaultCodeSpace());
            setConstantSpace(slgh.getConstantSpace());
            setUniqueSpace(slgh.getUniqueSpace());
            int4 num = slgh.numSpaces();
            for (int4 i = 0; i < num; ++i)
            {
                AddrSpace* spc = slgh.getSpace(i);
                spacetype type = spc.getType();
                if ((type == IPTR_CONSTANT) || (type == IPTR_PROCESSOR) || (type == IPTR_SPACEBASE) || (type == IPTR_INTERNAL))
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
            result = (ConstructTpl*)0;
            return res;
        }
        
        ~PcodeSnippet()
        {
            SymbolTree::iterator iter;
            for (iter = tree.begin(); iter != tree.end(); ++iter)
                delete* iter;       // Free ALL temporary symbols
            if (result != (ConstructTpl*)0)
            {
                delete result;
                result = (ConstructTpl*)0;
            }
        }

        public virtual Location getLocation(SleighSymbol sym) => (Location*)0;

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

        public void setUniqueBase(uint4 val)
        {
            tempbase = val;
        }

        public uint4 getUniqueBase() => tempbase;

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
            if (result != (ConstructTpl*)0)
            {
                delete result;
                result = (ConstructTpl*)0;
            }
            // tempbase = 0;
            errorcount = 0;
            firsterror.clear();
            resetLabelCount();
        }

        public int lex()
        {
            int4 tok = lexer.getNextToken();
            if (tok == STRING)
            {
                SleighSymbol* sym;
                SleighSymbol tmpsym(lexer.getIdentifier());
                SymbolTree::const_iterator iter = tree.find(&tmpsym);
                if (iter != tree.end())
                    sym = *iter;        // Found a local symbol
                else
                    sym = sleigh.findSymbol(lexer.getIdentifier());
                if (sym != (SleighSymbol*)0)
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
                yylval.i = new uintb(lexer.getNumber());
                return INTEGER;
            }
            return tok;
        }

        public bool parseStream(istream s)
        {
            lexer.initialize(&s);
            pcode = this;           // Setup global object for yyparse
            int4 res = yyparse();
            if (res != 0)
            {
                reportError((Location*)0,"Syntax error");
                return false;
            }
            if (!PcodeCompile::propagateSize(result))
            {
                reportError((Location*)0,"Could not resolve at least 1 variable size");
                return false;
            }
            return true;
        }

        public void addOperand(string name, int4 index)
        { // Add an operand symbol for this snippet
            OperandSymbol* sym = new OperandSymbol(name, index, (Constructor*)0);
            addSymbol(sym);
        }
    }
}
