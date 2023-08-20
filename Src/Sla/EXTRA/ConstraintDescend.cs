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
    internal class ConstraintDescend : UnifyConstraint
    {
        private int opindex;
        private int varindex;
        
        public ConstraintDescend(int oind, int vind)
        {
            opindex = oind;
            varindex = vind;
            maxnum = (opindex > varindex) ? opindex : varindex;
        }
        
        public override UnifyConstraint clone()
            => (new ConstraintDescend(opindex, varindex)).copyid(this);

        public override void buildTraverseState(UnifyState state)
        {
            if (uniqid != state.numTraverse())
                throw new LowlevelError("Traverse id does not match index");
            TraverseConstraint* newt = new TraverseDescendState(uniqid);
            state.registerTraverseConstraint(newt);
        }

        public override void initialize(UnifyState state)
        {
            TraverseDescendState* traverse = (TraverseDescendState*)state.getTraverse(uniqid);
            Varnode vn = state.data(varindex).getVarnode();
            traverse.initialize(vn);
        }

        public override bool step(UnifyState state)
        {
            TraverseDescendState* traverse = (TraverseDescendState*)state.getTraverse(uniqid);
            if (!traverse.step()) return false;
            PcodeOp op = traverse.getCurrentOp();
            state.data(opindex).setOp(op);
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
            s << "list<PcodeOp *>::const_iterator iter" << dec << printstate.getDepth() << ",enditer" << printstate.getDepth() << ';' << endl;
            printstate.printIndent(s);
            s << "iter" << printstate.getDepth() << " = " << printstate.getName(varindex) << ".beginDescend();" << endl;
            printstate.printIndent(s);
            s << "enditer" << printstate.getDepth() << " = " << printstate.getName(varindex) << ".endDescend();" << endl;
            printstate.printIndent(s);
            s << "while(iter" << printstate.getDepth() << " != enditer" << printstate.getDepth() << ") {" << endl;
            printstate.incDepth();  // permanent increase in depth
            printstate.printIndent(s);
            s << printstate.getName(opindex) << " = *iter" << (printstate.getDepth() - 1) << ';' << endl;
            printstate.printIndent(s);
            s << "++iter" << (printstate.getDepth() - 1) << endl;
        }
    }
}
