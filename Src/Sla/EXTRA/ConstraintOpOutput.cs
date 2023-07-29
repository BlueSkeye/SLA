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
    internal class ConstraintOpOutput : UnifyConstraint
    {
        // Move from op to its output varnode
        private int opindex;           // Which op
        private int varnodeindex;      // Label of output varnode
        
        public ConstraintOpOutput(int oind, int vind)
        {
            opindex = oind;
            varnodeindex = vind;
            maxnum = (opindex > varnodeindex) ? opindex : varnodeindex;
        }
        
        public override UnifyConstraint clone()
            => (new ConstraintOpOutput(opindex, varnodeindex)).copyid(this);

        public override bool step(UnifyState state)
        {
            TraverseCountState* traverse = (TraverseCountState*)state.getTraverse(uniqid);
            if (!traverse.step()) return false;
            PcodeOp* op = state.data(opindex).getOp();
            Varnode* vn = op.getOut();
            state.data(varnodeindex).setVarnode(vn);
            return true;
        }

        public override void collectTypes(List<UnifyDatatype> typelist)
        {
            typelist[opindex] = UnifyDatatype(UnifyDatatype::op_type);
            typelist[varnodeindex] = UnifyDatatype(UnifyDatatype::var_type);
        }

        public override int getBaseIndex() => varnodeindex;

        public override void print(TextWriter s, UnifyCPrinter printstate)
        {
            printstate.printIndent(s);
            s << printstate.getName(varnodeindex) << " = " << printstate.getName(opindex) << ".getOut();" << endl;
        }
    }
}
