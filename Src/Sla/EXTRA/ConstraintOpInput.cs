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
    internal class ConstraintOpInput : UnifyConstraint
    {
        // Move from op to one of its input varnodes
        private int4 opindex;           // Which op
        private int4 varnodeindex;      // Which input varnode
        private int4 slot;          // Which slot to take
        
        public ConstraintOpInput(int4 oind, int4 vind, int4 sl)
        {
            opindex = oind;
            varnodeindex = vind;
            slot = sl;
            maxnum = (opindex > varnodeindex) ? opindex : varnodeindex;
        }
        
        public override UnifyConstraint clone()
            => (new ConstraintOpInput(opindex, varnodeindex, slot)).copyid(this);

        public override bool step(UnifyState state)
        {
            TraverseCountState* traverse = (TraverseCountState*)state.getTraverse(uniqid);
            if (!traverse.step()) return false;
            PcodeOp* op = state.data(opindex).getOp();
            Varnode* vn = op.getIn(slot);
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
            s << printstate.getName(varnodeindex) << " = " << printstate.getName(opindex);
            s << ".getIn(" << dec << slot << ");" << endl;
        }
    }
}
