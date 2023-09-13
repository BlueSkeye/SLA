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
    internal class ConstraintSetInputConstVal : UnifyConstraint
    {
        private int opindex;
        private RHSConstant slot;
        private RHSConstant val;
        private RHSConstant? exprsz;
        
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
            if (exprsz != (RHSConstant)null)
                delete exprsz;
        }

        public override UnifyConstraint clone()
        {
            RHSConstant newexprsz = (RHSConstant)null;
            if (exprsz != (RHSConstant)null)
                newexprsz = exprsz.clone();
            UnifyConstraint res;
            res = (new ConstraintSetInputConstVal(opindex, slot.clone(), val.clone(), newexprsz)).copyid(this);
            return res;
        }

        public override bool step(UnifyState state)
        {
            TraverseCountState traverse = (TraverseCountState)state.getTraverse(uniqid);
            if (!traverse.step()) return false;
            Funcdata fd = state.getFunction();
            PcodeOp op = state.data(opindex).getOp();
            ulong ourconst = val.getConstant(state);
            int sz;
            if (exprsz != (RHSConstant)null)
                sz = (int)exprsz.getConstant(state);
            else
                sz = (int)sizeof(ulong);
            int slt = (int)slot.getConstant(state);
            fd.opSetInput(op, fd.newConstant(sz, ourconst & Globals.calc_mask((uint)sz)), slt);
            return true;
        }

        public override void collectTypes(List<UnifyDatatype> typelist)
        {
            typelist[opindex] = new UnifyDatatype(UnifyDatatype.TypeKind.op_type);
            //  typelist[varindex] = UnifyDatatype(UnifyDatatype.TypeKind.var_type);
        }

        public override void print(TextWriter s, UnifyCPrinter printstate)
        {
            printstate.printIndent(s);
            s.Write("data.opSetInput({printstate.getName(opindex)},data.newConstant(");
            if (exprsz != (RHSConstant)null)
                exprsz.writeExpression(s, printstate);
            else
                s.Write((int)sizeof(ulong));
            s.Write(",calc_mask(");
            if (exprsz != (RHSConstant)null)
                exprsz.writeExpression(s, printstate);
            else
                s.Write((int)sizeof(ulong));
            s.Write(")&");
            val.writeExpression(s, printstate);
            s.Write("),");
            slot.writeExpression(s, printstate);
            s.WriteLine(");");
        }
    }
}
