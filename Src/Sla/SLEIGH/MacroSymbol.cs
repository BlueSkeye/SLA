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
        private int index;
        private ConstructTpl construct;
        private List<OperandSymbol> operands;
        
        public MacroSymbol(string nm,int i)
            : base(nm)
        {
            index = i;
            construct = (ConstructTpl)null;
        }
        
        public int getIndex() => index;

        public void setConstruct(ConstructTpl ct)
        {
            construct = ct;
        }

        public ConstructTpl getConstruct() => construct;

        public void addOperand(OperandSymbol sym)
        {
            operands.Add(sym);
        }

        public int getNumOperands() => operands.size();

        public OperandSymbol getOperand(int i) => operands[i];

        ~MacroSymbol()
        {
            //if (construct != (ConstructTpl)null)
            //    delete construct;
        }

        public override symbol_type getType() => SleighSymbol.symbol_type.macro_symbol;
    }
}
