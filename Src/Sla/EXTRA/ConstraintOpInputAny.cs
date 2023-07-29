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
    internal class ConstraintOpInputAny : UnifyConstraint
    {
        // Move from op to ANY of its input varnodes
        private int4 opindex;           // Which op
        private int4 varnodeindex;      // What to label input varnode
        
        public ConstraintOpInputAny(int4 oind, int4 vind)
        {
            opindex = oind;
            varnodeindex = vind;
            maxnum = (opindex > varnodeindex) ? opindex : varnodeindex;
        }
        
        public override UnifyConstraint clone()
            => (new ConstraintOpInputAny(opindex, varnodeindex)).copyid(this);

        public override void initialize(UnifyState state)
        {               // Default initialization (with only 1 state)
            TraverseCountState* traverse = (TraverseCountState*)state.getTraverse(uniqid);
            PcodeOp* op = state.data(opindex).getOp();
            traverse.initialize(op.numInput());   // Initialize total number of inputs
        }

        public override bool step(UnifyState state)
        {
            TraverseCountState* traverse = (TraverseCountState*)state.getTraverse(uniqid);
            if (!traverse.step()) return false;
            PcodeOp* op = state.data(opindex).getOp();
            Varnode* vn = op.getIn(traverse.getState());
            state.data(varnodeindex).setVarnode(vn);
            return true;
        }

        public override void collectTypes(List<UnifyDatatype> typelist)
        {
            typelist[opindex] = UnifyDatatype(UnifyDatatype::op_type);
            typelist[varnodeindex] = UnifyDatatype(UnifyDatatype::var_type);
        }

        public override int4 getBaseIndex() => varnodeindex;

        public override void print(TextWriter s, UnifyCPrinter printstate)
        {
            printstate.printIndent(s);
            s << "for(int4 i" << dec << printstate.getDepth() << "=0;i" << printstate.getDepth() << '<';
            s << printstate.getName(opindex) << ".numInput();++i" << printstate.getDepth() << ") {" << endl;
            printstate.incDepth();  // A permanent increase in depth
            printstate.printIndent(s);
            s << printstate.getName(varnodeindex) << " = " << printstate.getName(opindex) << ".getIn(i";
            s << (printstate.getDepth() - 1) << ");" << endl;
        }
    }
}
