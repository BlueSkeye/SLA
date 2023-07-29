using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    internal class ConstraintBoolean : UnifyConstraint
    {
        // Constant expression must evaluate to true (or false)
        private bool istrue;
        private RHSConstant expr;
        
        public ConstraintBoolean(bool ist, RHSConstant ex)
        {
            istrue = ist;
            expr = ex;
            maxnum = -1;
        }

        ~ConstraintBoolean()
        {
            delete expr;
        }

        public override UnifyConstraint clone() => (new ConstraintBoolean(istrue, expr.clone())).copyid(this);

        public override bool step(UnifyState state);

        public override void print(TextWriter s, UnifyCPrinter printstate)
        {
            printstate.printIndent(s);
            s << "if (";
            expr.writeExpression(s, printstate);
            if (istrue)
                s << "== 0)";       // If false abort
            else
                s << "!= 0)";       // If true abort
            s << endl;
            printstate.printAbort(s);
        }
    }
}
