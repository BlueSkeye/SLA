﻿using Sla.DECCORE;

namespace Sla.EXTRA
{
    internal class ConstraintVarCompare : UnifyConstraint
    {
        private int var1index;
        private int var2index;
        private bool istrue;
        
        public ConstraintVarCompare(int var1ind, int var2ind, bool val)
        {
            var1index = var1ind;
            var2index = var2ind;
            istrue = val;
            maxnum = (var1index > var2index) ? var1index : var2index;
        }
        
        public override UnifyConstraint clone()
            => (new ConstraintVarCompare(var1index, var2index, istrue)).copyid(this);

        public override bool step(UnifyState state)
        {
            TraverseCountState traverse = (TraverseCountState)state.getTraverse(uniqid);
            if (!traverse.step()) return false;
            Varnode vn1 = state.data(var1index).getVarnode();
            Varnode vn2 = state.data(var2index).getVarnode();
            return ((vn1 == vn2) == istrue);
        }

        public override void collectTypes(List<UnifyDatatype> typelist)
        {
            typelist[var1index] = new UnifyDatatype(UnifyDatatype.TypeKind.var_type);
            typelist[var2index] = new UnifyDatatype(UnifyDatatype.TypeKind.var_type);
        }

        public override int getBaseIndex() => var1index;

        public override void print(TextWriter s, UnifyCPrinter printstate)
        {
            printstate.printIndent(s);
            s.Write($"if ({printstate.getName(var1index)}");
            if (istrue)
                s.Write(" != ");
            else
                s.Write(" == ");
            s.WriteLine($"{printstate.getName(var2index)})");
            printstate.printAbort(s);
        }
    }
}
