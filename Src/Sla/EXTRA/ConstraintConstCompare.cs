using Sla.CORE;

namespace Sla.EXTRA
{
    internal class ConstraintConstCompare : UnifyConstraint
    {
        private int const1index;       // Compare two constants resulting in a boolean
        private int const2index;
        private OpCode opc;
        
        public ConstraintConstCompare(int c1ind, int c2ind, OpCode oc)
        {
            const1index = c1ind; const2index = c2ind; opc = oc;
            maxnum = (const1index > const2index) ? const1index : const2index;
        }

        public override UnifyConstraint clone() 
            => (new ConstraintConstCompare(const1index, const2index, opc)).copyid(this);

        public override bool step(UnifyState state)
        {
            TraverseCountState traverse = (TraverseCountState*)state.getTraverse(uniqid);
            if (!traverse.step()) return false;
            ulong c1 = state.data(const1index).getConstant();
            ulong c2 = state.data(const2index).getConstant();
            // This only does operations with boolean result
            OpBehavior behavior = state.getBehavior(opc);
            ulong res = behavior.evaluateBinary(1, sizeof(ulong), c1, c2);
            return (res != 0);
        }

        public override void collectTypes(List<UnifyDatatype> typelist)
        {
            typelist[const1index] = new UnifyDatatype(UnifyDatatype.TypeKind.const_type);
            typelist[const2index] = new UnifyDatatype(UnifyDatatype.TypeKind.const_type);
        }

        public override int getBaseIndex() => const1index;

        public override void print(TextWriter s, UnifyCPrinter printstate)
        {
            printstate.printIndent(s);
            s.Write("if (");
            switch (opc) {
                case OpCode.CPUI_INT_EQUAL:
                    s.Write($"{printstate.getName(const1index)} != {printstate.getName(const2index)}");
                    break;
                case OpCode.CPUI_INT_NOTEQUAL:
                    s.Write($"{printstate.getName(const1index)} == {printstate.getName(const2index)}");
                    break;
                default:
                    s.Write("/* unimplemented constant operation */");
                    break;
            }
            s.WriteLine(')');
            printstate.printAbort(s);
        }
    }
}
