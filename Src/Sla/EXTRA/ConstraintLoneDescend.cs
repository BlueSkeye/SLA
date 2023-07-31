using Sla.DECCORE;
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
    internal class ConstraintLoneDescend : UnifyConstraint
    {
        private int opindex;
        private int varindex;
        
        public ConstraintLoneDescend(int oind, int vind)
        {
            opindex = oind;
            varindex = vind;
            maxnum = (opindex > varindex) ? opindex : varindex;
        }
        
        public override UnifyConstraint clone() => (new ConstraintLoneDescend(opindex, varindex)).copyid(this);

        public override bool step(UnifyState state)
        {
            TraverseCountState* traverse = (TraverseCountState*)state.getTraverse(uniqid);
            if (!traverse.step()) return false;
            Varnode* vn = state.data(varindex).getVarnode();
            PcodeOp* res = vn.loneDescend();
            if (res == (PcodeOp)null) return false;
            state.data(opindex).setOp(res);
            return true;
        }

        public override void collectTypes(List<UnifyDatatype> typelist)
        {
            typelist[opindex] = UnifyDatatype(UnifyDatatype.TypeKind.op_type);
            typelist[varindex] = UnifyDatatype(UnifyDatatype.TypeKind.var_type);
        }

        public override int getBaseIndex() => opindex;

        public override void print(TextWriter s, UnifyCPrinter printstate)
        {
            printstate.printIndent(s);
            s << printstate.getName(opindex) << " = " << printstate.getName(varindex) << ".loneDescend();" << endl;
            printstate.printIndent(s);
            s << "if (" << printstate.getName(opindex) << " == (PcodeOp *)0)" << endl;
            printstate.printAbort(s);
        }
    }
}
