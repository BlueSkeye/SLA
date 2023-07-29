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
    internal class ConstraintRemoveInput : UnifyConstraint
    {
        private int4 opindex;
        private RHSConstant slot;
        
        public ConstraintRemoveInput(int4 oind, RHSConstant sl)
        {
            opindex = oind;
            slot = sl;
            maxnum = opindex;
        }

        ~ConstraintRemoveInput()
        {
            delete slot;
        }

        public override UnifyConstraint clone() 
            => (new ConstraintRemoveInput(opindex, slot->clone()))->copyid(this);

        public override int4 getBaseIndex() => opindex;

        public override bool step(UnifyState state)
        {
            TraverseCountState* traverse = (TraverseCountState*)state.getTraverse(uniqid);
            if (!traverse->step()) return false;
            Funcdata* fd = state.getFunction();
            PcodeOp* op = state.data(opindex).getOp();
            int4 slt = (int4)slot->getConstant(state);
            fd->opRemoveInput(op, slt);
            return true;
        }

        public override void collectTypes(List<UnifyDatatype> typelist)
        {
            typelist[opindex] = UnifyDatatype(UnifyDatatype::op_type);
        }

        public override void print(TextWriter s, UnifyCPrinter printstate)
        {
            printstate.printIndent(s);
            s << "data.opRemoveInput(" << printstate.getName(opindex) << ',';
            slot->writeExpression(s, printstate);
            s << ");" << endl;
        }
    }
}
