using Sla.SLEIGH;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Text;
using System.Threading.Tasks;

namespace Sla.SLACOMP
{
    internal class /*union*/ SLEIGHSTYPE
    {
        internal char ch;
        internal ulong i;
        internal long big;
        internal string str;
        internal List<string> strlist;
        internal List<long> biglist;
        internal List<ExprTree> param;
        internal SpaceQuality spacequal;
        internal FieldQuality fieldqual;
        internal StarQuality starqual;
        internal VarnodeTpl varnode;
        internal ExprTree tree;
        internal List<OpTpl> stmt;
        internal ConstructTpl sem;
        internal SectionVector sectionstart;
        internal Constructor construct;
        internal PatternEquation pateq;
        internal PatternExpression patexp;

        internal List<SleighSymbol> symlist;
        internal List<ContextChange> contop;
        internal SleighSymbol anysym;
        internal SpaceSymbol spacesym;
        internal SectionSymbol sectionsym;
        internal TokenSymbol tokensym;
        internal UserOpSymbol useropsym;
        internal MacroSymbol macrosym;
        internal LabelSymbol labelsym;
        internal SubtableSymbol subtablesym;
        internal StartSymbol startsym;
        internal EndSymbol endsym;
        internal Next2Symbol next2sym;
        internal OperandSymbol operandsym;
        internal VarnodeListSymbol varlistsym;
        internal VarnodeSymbol varsym;
        internal BitrangeSymbol bitsym;
        internal NameSymbol namesym;
        internal ValueSymbol valuesym;
        internal ValueMapSymbol valuemapsym;
        internal ContextSymbol contextsym;
        internal FamilySymbol famsym;
        internal SpecificSymbol specsym;
    }
}
