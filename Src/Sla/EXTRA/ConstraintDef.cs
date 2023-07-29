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
    internal class ConstraintDef : UnifyConstraint
    {
        private int opindex;           // Where to store defining op
        private int varindex;      // Which varnode to examine for def
        
        public ConstraintDef(int oind, int vind)
        {
            opindex = oind;
            varindex = vind;
            maxnum = (opindex > varindex) ? opindex : varindex;
        }
        
        public override UnifyConstraint clone() 
            => (new ConstraintDef(opindex, varindex)).copyid(this);

        public override bool step(UnifyState state)
        {
            TraverseCountState* traverse = (TraverseCountState*)state.getTraverse(uniqid);
            if (!traverse.step()) return false;
            Varnode* vn = state.data(varindex).getVarnode();
            if (!vn.isWritten()) return false;
            PcodeOp* op = vn.getDef();
            state.data(opindex).setOp(op);
            return true;
        }

        public override void collectTypes(List<UnifyDatatype> typelist)
        {
            typelist[opindex] = UnifyDatatype(UnifyDatatype::op_type);
            typelist[varindex] = UnifyDatatype(UnifyDatatype::var_type);
        }

        public override int getBaseIndex() => opindex;

        public override void print(TextWriter s, UnifyCPrinter printstate)
        {
            printstate.printIndent(s);
            s << "if (!" << printstate.getName(varindex) << ".isWritten())" << endl;
            printstate.printAbort(s);
            printstate.printIndent(s);
            s << printstate.getName(opindex) << " = " << printstate.getName(varindex) << ".getDef();" << endl;
        }
    }
}
