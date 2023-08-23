using Sla.DECCORE;

namespace Sla.EXTRA
{
    internal class ConstraintVarConst : UnifyConstraint
    {
        // Create a new varnode constant
        private int varindex;
        private RHSConstant expr;
        private RHSConstant exprsz;
        
        public ConstraintVarConst(int ind, RHSConstant ex, RHSConstant sz)
        {
            varindex = ind;
            maxnum = ind;
            expr = ex;
            exprsz = sz;
        }
        
        ~ConstraintVarConst()
        {
            //delete expr;
            //if (exprsz != (RHSConstant)null)
            //    delete exprsz;
        }

        public override UnifyConstraint clone()
        {
            UnifyConstraint res;
            RHSConstant? newexprsz = (RHSConstant)null;
            if (exprsz != (RHSConstant)null)
                newexprsz = exprsz.clone();
            res = (new ConstraintVarConst(varindex, expr.clone(), newexprsz)).copyid(this);
            return res;
        }

        public override bool step(UnifyState state)
        {
            TraverseCountState traverse = (TraverseCountState)state.getTraverse(uniqid);
            if (!traverse.step()) return false;
            ulong ourconst = expr.getConstant(state);
            Funcdata fd = state.getFunction();
            int sz;
            if (exprsz != (RHSConstant)null)
                sz = (int)exprsz.getConstant(state);
            else
                sz = (int)sizeof(ulong);
            ourconst &= Globals.calc_mask(sz);
            Varnode vn = fd.newConstant(sz, ourconst);
            state.data(varindex).setVarnode(vn);
            return true;
        }

        public override void collectTypes(List<UnifyDatatype> typelist)
        {
            typelist[varindex] = new UnifyDatatype(UnifyDatatype.TypeKind.var_type);
        }

        public override int getBaseIndex() => varindex;

        public override void print(TextWriter s, UnifyCPrinter printstate)
        {
            printstate.printIndent(s);
            s.Write($"{printstate.getName(varindex)} = data.newConstant(");
            if (exprsz != (RHSConstant)null)
                exprsz.writeExpression(s, printstate);
            else
                s.Write((int)sizeof(ulong));
            s.Write(',');
            expr.writeExpression(s, printstate);
            s.Write(" & Globals.calc_mask(");
            exprsz.writeExpression(s, printstate);
            s.WriteLine("));");
        }
    }
}
