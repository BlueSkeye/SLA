using Sla.SLEIGH;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Text;
using System.Threading.Tasks;
using static Sla.SLEIGH.SleighSymbol;

namespace Sla.SLEIGH
{
    internal class MacroSymbol : SleighSymbol
    {
        // A user-defined pcode-macro
        private int4 index;
        private ConstructTpl construct;
        private List<OperandSymbol> operands;
        
        public MacroSymbol(string nm,int4 i)
            : base(nm)
        {
            index = i;
            construct = (ConstructTpl*)0;
        }
        
        public int4 getIndex() => index;

        public void setConstruct(ConstructTpl ct)
        {
            construct = ct;
        }

        public ConstructTpl getConstruct() => construct;

        public void addOperand(OperandSymbol sym)
        {
            operands.push_back(sym);
        }

        public int4 getNumOperands() => operands.size();

        public OperandSymbol getOperand(int4 i) => operands[i];

        ~MacroSymbol()
        {
            if (construct != (ConstructTpl*)0) delete construct;
        }

        public override symbol_type getType() => macro_symbol;
    }
}
