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
    internal class ConstraintSetOpcode : UnifyConstraint
    {
        private int opindex;
        private OpCode opc;
        
        public ConstraintSetOpcode(int oind, OpCode oc)
        {
            opindex = oind;
            opc = oc;
            maxnum = opindex;
        }
        
        public override UnifyConstraint clone() 
            => (new ConstraintSetOpcode(opindex, opc)).copyid(this);

        public override int getBaseIndex() => opindex;

        public override bool step(UnifyState state)
        {
            TraverseCountState* traverse = (TraverseCountState*)state.getTraverse(uniqid);
            if (!traverse.step()) return false;
            Funcdata* fd = state.getFunction();
            PcodeOp op = state.data(opindex).getOp();
            fd.opSetOpcode(op, opc);
            return true;
        }

        public override void collectTypes(List<UnifyDatatype> typelist)
        {
            typelist[opindex] = UnifyDatatype(UnifyDatatype.TypeKind.op_type);
        }

        public override void print(TextWriter s, UnifyCPrinter printstate)
        {
            printstate.printIndent(s);
            s << "data.opSetOpcode(" << printstate.getName(opindex) << ",CPUI_" << Globals.get_opname(opc) << ");" << endl;
        }
    }
}
