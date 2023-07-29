﻿using Sla.DECCORE;
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
    internal class ConstraintOpCopy : UnifyConstraint
    {
        private int4 oldopindex;
        private int4 newopindex;
        
        public ConstraintOpCopy(int4 oldind, int4 newind)
        {
            oldopindex = oldind;
            newopindex = newind;
            maxnum = (oldopindex > newopindex) ? oldopindex : newopindex;
        }
        
        public override UnifyConstraint clone() => (new ConstraintOpCopy(oldopindex, newopindex))->copyid(this);

        public override bool step(UnifyState state)
        {
            TraverseCountState* traverse = (TraverseCountState*)state.getTraverse(uniqid);
            if (!traverse->step()) return false;
            PcodeOp* op = state.data(oldopindex).getOp();
            state.data(newopindex).setOp(op);
            return true;
        }

        public override void collectTypes(List<UnifyDatatype> typelist)
        {
            typelist[oldopindex] = UnifyDatatype(UnifyDatatype::op_type);
            typelist[newopindex] = UnifyDatatype(UnifyDatatype::op_type);
        }

        public override int4 getBaseIndex() => oldopindex;

        public override void print(TextWriter s, UnifyCPrinter printstate)
        {
            printstate.printIndent(s);
            s << printstate.getName(newopindex) << " = " << printstate.getName(oldopindex) << ';' << endl;
        }
    }
}