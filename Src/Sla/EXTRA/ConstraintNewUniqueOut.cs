﻿using Sla.DECCORE;
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
    internal class ConstraintNewUniqueOut : UnifyConstraint
    {
        private int4 opindex;
        private int4 newvarindex;
        private int4 sizevarindex;      // Negative is specific size, Positive is varnode index (for size)
        
        public ConstraintNewUniqueOut(int4 oind, int4 newvarind, int4 sizeind)
        {
            opindex = oind;
            newvarindex = newvarind;
            sizevarindex = sizeind;
            maxnum = (opindex > newvarindex) ? opindex : newvarindex;
            if (sizevarindex > maxnum)
                maxnum = sizevarindex;
        }

        public override UnifyConstraint clone()
            => (new ConstraintNewUniqueOut(opindex, newvarindex, sizevarindex))->copyid(this);

        public override int4 getBaseIndex() => newvarindex;

        public override bool step(UnifyState state)
        {
            TraverseCountState* traverse = (TraverseCountState*)state.getTraverse(uniqid);
            if (!traverse->step()) return false;
            Funcdata* fd = state.getFunction();
            PcodeOp* op = state.data(opindex).getOp();
            int4 sz;
            if (sizevarindex < 0)
                sz = -sizevarindex;     // A specific size
            else
            {
                Varnode* sizevn = state.data(sizevarindex).getVarnode();
                sz = sizevn->getSize();
            }
            Varnode* newvn = fd->newUniqueOut(sz, op);
            state.data(newvarindex).setVarnode(newvn);
            return true;
        }

        public override void collectTypes(List<UnifyDatatype> typelist)
        {
            typelist[opindex] = UnifyDatatype(UnifyDatatype::op_type);
            typelist[newvarindex] = UnifyDatatype(UnifyDatatype::var_type);
            if (sizevarindex >= 0)
                typelist[sizevarindex] = UnifyDatatype(UnifyDatatype::var_type);
        }

        public override void print(TextWriter s, UnifyCPrinter printstate)
        {
            printstate.printIndent(s);
            s << printstate.getName(newvarindex) << " = data.newUniqueOut(";
            if (sizevarindex < 0)
                s << dec << -sizevarindex;
            else
                s << printstate.getName(sizevarindex) << "->getSize()";
            s << ',' << printstate.getName(opindex) << ");" << endl;
        }
    }
}