
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
            // delete expr;
        }

        public override UnifyConstraint clone() => (new ConstraintBoolean(istrue, expr.clone())).copyid(this);

        public override bool step(UnifyState state)
        {
            TraverseCountState traverse = (TraverseCountState)state.getTraverse(uniqid);
            if (!traverse.step()) return false;
            ulong ourconst = expr.getConstant(state);
            return (istrue) ? (ourconst != 0) : (ourconst == 0);
        }

        public override void print(TextWriter s, UnifyCPrinter printstate)
        {
            printstate.printIndent(s);
            s.Write("if (");
            expr.writeExpression(s, printstate);
            if (istrue)
                s.Write("== 0)");       // If false abort
            else
                s.Write("!= 0)");       // If true abort
            s.WriteLine();
            printstate.printAbort(s);
        }
    }
}
