using Sla.DECCORE;
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
    internal class ConstraintSetInput : UnifyConstraint
    {
        private int opindex;
        private RHSConstant slot;
        private int varindex;
        
        public ConstraintSetInput(int oind, RHSConstant sl, int varind)
        {
            opindex = oind;
            slot = sl;
            varindex = varind;
            maxnum = (opindex > varindex) ? opindex : varindex;
        }

        ~ConstraintSetInput()
        {
            delete slot;
        }

        public override UnifyConstraint clone() 
            => (new ConstraintSetInput(opindex, slot.clone(), varindex)).copyid(this);

        public override int getBaseIndex() => varindex;

        public override bool step(UnifyState state)
        {
            TraverseCountState* traverse = (TraverseCountState*)state.getTraverse(uniqid);
            if (!traverse.step()) return false;
            Funcdata* fd = state.getFunction();
            PcodeOp op = state.data(opindex).getOp();
            Varnode vn = state.data(varindex).getVarnode();
            int slt = (int)slot.getConstant(state);
            fd.opSetInput(op, vn, slt);
            return true;
        }

        public override void collectTypes(List<UnifyDatatype> typelist)
        {
            typelist[opindex] = UnifyDatatype(UnifyDatatype.TypeKind.op_type);
            typelist[varindex] = UnifyDatatype(UnifyDatatype.TypeKind.var_type);
        }

        public override void print(TextWriter s, UnifyCPrinter printstate)
        {
            printstate.printIndent(s);
            s << "data.opSetInput(" << printstate.getName(opindex) << ',' << printstate.getName(varindex);
            s << ',';
            slot.writeExpression(s, printstate);
            s << ");" << endl;
        }
    }
}
