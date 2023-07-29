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
    internal class ConstraintOpCompare : UnifyConstraint
    {
        private int4 op1index;
        private int4 op2index;
        private bool istrue;
        
        public ConstraintOpCompare(int4 op1ind, int4 op2ind, bool val)
        {
            op1index = op1ind;
            op2index = op2ind;
            istrue = val;
            maxnum = (op1index > op2index) ? op1index : op2index;
        }
        
        public override UnifyConstraint clone()
            => (new ConstraintOpCompare(op1index, op2index, istrue)).copyid(this);

        public override bool step(UnifyState state)
        {
            TraverseCountState* traverse = (TraverseCountState*)state.getTraverse(uniqid);
            if (!traverse.step()) return false;
            PcodeOp* op1 = state.data(op1index).getOp();
            PcodeOp* op2 = state.data(op2index).getOp();
            return ((op1 == op2) == istrue);
        }

        public override void collectTypes(List<UnifyDatatype> typelist)
        {
            typelist[op1index] = UnifyDatatype(UnifyDatatype::op_type);
            typelist[op2index] = UnifyDatatype(UnifyDatatype::op_type);
        }

        public override int4 getBaseIndex() => op1index;

        public override void print(TextWriter s, UnifyCPrinter printstate)
        {
            printstate.printIndent(s);
            s << "if (" << printstate.getName(op1index);
            if (istrue)
                s << " != ";
            else
                s << " == ";
            s << printstate.getName(op2index) << ')' << endl;
            printstate.printAbort(s);
        }
    }
}
