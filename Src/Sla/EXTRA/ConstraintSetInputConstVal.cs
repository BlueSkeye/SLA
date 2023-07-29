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
    internal class ConstraintSetInputConstVal : UnifyConstraint
    {
        private int opindex;
        private RHSConstant slot;
        private RHSConstant val;
        private RHSConstant exprsz;
        
        public ConstraintSetInputConstVal(int oind, RHSConstant sl, RHSConstant v, RHSConstant sz)
        {
            opindex = oind;
            slot = sl;
            val = v;
            exprsz = sz;
            maxnum = opindex;
        }
        
        ~ConstraintSetInputConstVal()
        {
            delete val;
            delete slot;
            if (exprsz != (RHSConstant*)0)
                delete exprsz;
        }

        public override UnifyConstraint clone()
        {
            RHSConstant* newexprsz = (RHSConstant*)0;
            if (exprsz != (RHSConstant*)0)
                newexprsz = exprsz.clone();
            UnifyConstraint* res;
            res = (new ConstraintSetInputConstVal(opindex, slot.clone(), val.clone(), newexprsz)).copyid(this);
            return res;
        }

        public override bool step(UnifyState state)
        {
            TraverseCountState* traverse = (TraverseCountState*)state.getTraverse(uniqid);
            if (!traverse.step()) return false;
            Funcdata* fd = state.getFunction();
            PcodeOp* op = state.data(opindex).getOp();
            ulong ourconst = val.getConstant(state);
            int sz;
            if (exprsz != (RHSConstant*)0)
                sz = (int)exprsz.getConstant(state);
            else
                sz = (int)sizeof(ulong);
            int slt = (int)slot.getConstant(state);
            fd.opSetInput(op, fd.newConstant(sz, ourconst & Globals.calc_mask(sz)), slt);
            return true;
        }

        public override void collectTypes(List<UnifyDatatype> typelist)
        {
            typelist[opindex] = UnifyDatatype(UnifyDatatype::op_type);
            //  typelist[varindex] = UnifyDatatype(UnifyDatatype::var_type);
        }

        public override void print(TextWriter s, UnifyCPrinter printstate)
        {
            printstate.printIndent(s);
            s << "data.opSetInput(" << printstate.getName(opindex) << ",data.newConstant(";
            if (exprsz != (RHSConstant*)0)
                exprsz.writeExpression(s, printstate);
            else
                s << dec << (int)sizeof(ulong);
            s << ",calc_mask(";
            if (exprsz != (RHSConstant*)0)
                exprsz.writeExpression(s, printstate);
            else
                s << dec << (int)sizeof(ulong);
            s << ")&";
            val.writeExpression(s, printstate);
            s << "),";
            slot.writeExpression(s, printstate);
            s << ");" << endl;
        }
    }
}
