using Sla.DECCORE;
using Sla.EXTRA;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection.Emit;
using System.Runtime.Intrinsics;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    internal class ConstraintOpcode : UnifyConstraint
    {
        private int opindex;
        private List<OpCode> opcodes; // Which opcodes match
        
        public ConstraintOpcode(int ind, List<OpCode> o)
        {
            maxnum = opindex = ind;
            opcodes = o;
        }
    
        public List<OpCode> getOpCodes() => opcodes;

        public override UnifyConstraint clone() => (new ConstraintOpcode(opindex, opcodes)).copyid(this);

        public override bool step(UnifyState state)
        {
            TraverseCountState* traverse = (TraverseCountState*)state.getTraverse(uniqid);
            if (!traverse.step()) return false;
            PcodeOp op = state.data(opindex).getOp();
            for (int i = 0; i < opcodes.size(); ++i)
                if (op.code() == opcodes[i]) return true;
            return false;
        }

        public override void collectTypes(List<UnifyDatatype> typelist)
        {
            typelist[opindex] = UnifyDatatype(UnifyDatatype.TypeKind.op_type);
        }

        public override int getBaseIndex() => opindex;

        public override void print(TextWriter s, UnifyCPrinter printstate)
        {
            printstate.printIndent(s);
            s << "if (";
            if (opcodes.size() == 1)
            {
                s << printstate.getName(opindex) << ".code() != CPUI_" << Globals.get_opname(opcodes[0]);
            }
            else
            {
                s << '(' << printstate.getName(opindex) << ".code() != CPUI_" << Globals.get_opname(opcodes[0]) << ')';
                for (int i = 1; i < opcodes.size(); ++i)
                {
                    s << "&&";
                    s << '(' << printstate.getName(opindex) << ".code() != CPUI_" << Globals.get_opname(opcodes[i]) << ')';
                }
            }
            s << ')' << endl;
            printstate.printAbort(s);
        }
    }
}
