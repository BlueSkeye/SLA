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
    internal class ConstraintOtherInput : UnifyConstraint
    {
        private int opindex;           // For a particular binary op
        private int varindex_in;       // Given one of its input varnodes
        private int varindex_out;      // Label the other input to op
        
        public ConstraintOtherInput(int oind, int v_in, int v_out)
        {
            maxnum = opindex = oind; varindex_in = v_in; varindex_out = v_out;
            if (varindex_in > maxnum) maxnum = varindex_in; if (varindex_out > maxnum) maxnum = varindex_out;
        }
        
        public override UnifyConstraint clone()
            => (new ConstraintOtherInput(opindex, varindex_in, varindex_out)).copyid(this);

        public override int getBaseIndex() => varindex_out;

        public override bool step(UnifyState state)
        {
            TraverseCountState* traverse = (TraverseCountState*)state.getTraverse(uniqid);
            if (!traverse.step()) return false;
            PcodeOp op = state.data(opindex).getOp();
            Varnode vn = state.data(varindex_in).getVarnode();
            Varnode res = op.getIn(1 - op.getSlot(vn)); // Get the "other" input
            state.data(varindex_out).setVarnode(res);
            return true;
        }

        public override void collectTypes(List<UnifyDatatype> typelist)
        {
            typelist[opindex] = UnifyDatatype(UnifyDatatype.TypeKind.op_type);
            typelist[varindex_in] = UnifyDatatype(UnifyDatatype.TypeKind.var_type);
            typelist[varindex_out] = UnifyDatatype(UnifyDatatype.TypeKind.var_type);
        }

        public override void print(TextWriter s, UnifyCPrinter printstate)
        {
            printstate.printIndent(s);
            s << printstate.getName(varindex_out) << " = " << printstate.getName(opindex) << ".getIn(1 - ";
            s << printstate.getName(opindex) << ".getSlot(" << printstate.getName(varindex_in) << "));" << endl;
        }
    }
}
