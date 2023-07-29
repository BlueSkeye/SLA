using Sla.EXTRA;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    internal class ConstraintNamedExpression : UnifyConstraint
    {
        private int constindex;
        private RHSConstant expr;
        
        public ConstraintNamedExpression(int ind, RHSConstant ex)
        {
            constindex = ind;
            expr = ex;
            maxnum = constindex;
        }

        ~ConstraintNamedExpression()
        {
            delete expr;
        }

        public override UnifyConstraint clone()
            => (new ConstraintNamedExpression(constindex, expr.clone())).copyid(this);

        public override bool step(UnifyState state)
        {
            TraverseCountState* traverse = (TraverseCountState*)state.getTraverse(uniqid);
            if (!traverse.step()) return false;
            ulong ourconst = expr.getConstant(state);
            state.data(constindex).setConstant(ourconst);
            return true;
        }

        public override void collectTypes(List<UnifyDatatype> typelist)
        {
            typelist[constindex] = UnifyDatatype(UnifyDatatype::const_type);
        }

        public override int getBaseIndex() => constindex;

        public override void print(TextWriter s, UnifyCPrinter printstate)
        {
            printstate.printIndent(s);
            s << printstate.getName(constindex) << " = ";
            expr.writeExpression(s, printstate);
            s << ';' << endl;
        }
    }
}
