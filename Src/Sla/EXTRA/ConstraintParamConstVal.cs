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
    internal class ConstraintParamConstVal : UnifyConstraint
    {
        private int opindex;           // Which opcode
        private int slot;          // Which slot to examine for constant
        private ulong val;          // What value parameter must match
        
        public ConstraintParamConstVal(int oind, int sl, ulong v)
        {
            maxnum = opindex = oind;
            slot = sl;
            val = v;
        }
        
        public override UnifyConstraint clone()
            => (new ConstraintParamConstVal(opindex, slot, val)).copyid(this);

        public override bool step(UnifyState state)
        {
            TraverseCountState* traverse = (TraverseCountState*)state.getTraverse(uniqid);
            if (!traverse.step()) return false;
            PcodeOp* op = state.data(opindex).getOp();
            Varnode* vn = op.getIn(slot);
            if (!vn.isConstant()) return false;
            if (vn.getOffset() != (val & calc_mask(vn.getSize()))) return false;
            return true;
        }

        public override void collectTypes(List<UnifyDatatype> typelist)
        {
            typelist[opindex] = UnifyDatatype(UnifyDatatype::op_type);
        }

        public override void print(TextWriter s, UnifyCPrinter printstate)
        {
            printstate.printIndent(s);
            s << "if (!" << printstate.getName(opindex) << ".getIn(" << dec << slot << ").isConstant())" << endl;
            printstate.printAbort(s);
            printstate.printIndent(s);
            s << "if (" << printstate.getName(opindex) << ".getIn(" << dec << slot << ").getOffset() != 0x";
            s << hex << val << " & calc_mask(" << printstate.getName(opindex) << ".getIn(" << dec;
            s << slot << ").getSize()))" << endl;
            printstate.printAbort(s);
        }
    }
}
