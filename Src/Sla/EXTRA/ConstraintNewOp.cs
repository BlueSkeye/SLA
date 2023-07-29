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
    // Action constraints, these must always step exactly once (returning true), and do their action
    internal class ConstraintNewOp : UnifyConstraint
    {
        private int4 newopindex;
        private int4 oldopindex;
        private bool insertafter;       // true if inserted AFTER oldop
        private OpCode opc;         // new opcode
        private int4 numparams;
        
        public ConstraintNewOp(int4 newind, int4 oldind, OpCode oc, bool iafter, int4 num)
        {
            newopindex = newind;
            oldopindex = oldind;
            opc = oc;
            insertafter = iafter;
            numparams = num;
            maxnum = (newind > oldind) ? newind : oldind;
        }

        public override UnifyConstraint clone()
            => (new ConstraintNewOp(newopindex, oldopindex, opc, insertafter, numparams)).copyid(this);

        public override int4 getBaseIndex() => newopindex;

        public override bool step(UnifyState state)
        {
            TraverseCountState* traverse = (TraverseCountState*)state.getTraverse(uniqid);
            if (!traverse.step()) return false;
            Funcdata* fd = state.getFunction();
            PcodeOp* op = state.data(oldopindex).getOp();
            PcodeOp* newop = fd.newOp(numparams, op.getAddr());
            fd.opSetOpcode(newop, opc);
            if (insertafter)
                fd.opInsertAfter(newop, op);
            else
                fd.opInsertBefore(newop, op);
            return true;
        }

        public override void collectTypes(List<UnifyDatatype> typelist)
        {
            typelist[newopindex] = UnifyDatatype(UnifyDatatype::op_type);
            typelist[oldopindex] = UnifyDatatype(UnifyDatatype::op_type);
        }

        public override void print(TextWriter s, UnifyCPrinter printstate)
        {
            printstate.printIndent(s);
            s << printstate.getName(newopindex) << " = data.newOp(" << dec << numparams;
            s << ',' << printstate.getName(oldopindex) << ".getAddr());" << endl;
            printstate.printIndent(s);
            s << "data.opSetOpcode(" << printstate.getName(newopindex) << ",CPUI_" << get_opname(opc) << ");" << endl;
            s << "data.opInsert";
            if (insertafter)
                s << "After(";
            else
                s << "Before(";
            s << printstate.getName(newopindex) << ',' << printstate.getName(oldopindex) << ");" << endl;
        }
    }
}
