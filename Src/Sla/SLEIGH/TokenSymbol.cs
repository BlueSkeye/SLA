using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Sla.SLEIGH.SleighSymbol;

namespace Sla.SLEIGH
{
    internal class TokenSymbol : SleighSymbol
    {
        private Token tok;
        
        public TokenSymbol(Token t)
            : base(t.getName())
        {
            tok = t;
        }
        
        ~TokenSymbol()
        {
            delete tok;
        }
    
        public Token getToken() => tok;

        public override symbol_type getType() => SleighSymbol.symbol_type.token_symbol;
    }
}
