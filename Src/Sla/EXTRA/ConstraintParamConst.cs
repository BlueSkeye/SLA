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
    internal class ConstraintParamConst : UnifyConstraint
    {
        private int4 opindex;           // Which opcode
        private int4 slot;          // Which slot to examine for constant
        private int4 constindex;        // Which varnode is the constant
        
        public ConstraintParamConst(int4 oind, int4 sl, int4 cind)
        {
            opindex = oind;
            slot = sl;
            constindex = cind;
            maxnum = (opindex > constindex) ? opindex : constindex;
        }
        
        public override UnifyConstraint clone()
            => (new ConstraintParamConst(opindex, slot, constindex)).copyid(this);

        public override bool step(UnifyState state)
        {
            TraverseCountState* traverse = (TraverseCountState*)state.getTraverse(uniqid);
            if (!traverse.step()) return false;
            PcodeOp* op = state.data(opindex).getOp();
            Varnode* vn = op.getIn(slot);
            if (!vn.isConstant()) return false;
            state.data(constindex).setConstant(vn.getOffset());
            return true;
        }

        public override void collectTypes(List<UnifyDatatype> typelist)
        {
            typelist[opindex] = UnifyDatatype(UnifyDatatype::op_type);
            typelist[constindex] = UnifyDatatype(UnifyDatatype::const_type);
        }

        public override int4 getBaseIndex() => constindex;

        public override void print(TextWriter s, UnifyCPrinter printstate)
        {
            printstate.printIndent(s);
            s << "if (!" << printstate.getName(opindex) << ".getIn(" << dec << slot << ").isConstant())" << endl;
            printstate.printAbort(s);
            printstate.printIndent(s);
            s << printstate.getName(constindex) << " = ";
            s << printstate.getName(opindex) << ".getIn(" << dec << slot << ").getOffset();" << endl;
        }
    }
}
