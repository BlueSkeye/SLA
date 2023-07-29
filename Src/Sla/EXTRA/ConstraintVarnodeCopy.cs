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
    internal class ConstraintVarnodeCopy : UnifyConstraint
    {
        private int4 oldvarindex;
        private int4 newvarindex;
        
        public ConstraintVarnodeCopy(int4 oldind, int4 newind)
        {
            oldvarindex = oldind;
            newvarindex = newind;
            maxnum = (oldvarindex > newvarindex) ? oldvarindex : newvarindex;
        }
        
        public override UnifyConstraint clone()
            => (new ConstraintVarnodeCopy(oldvarindex, newvarindex))->copyid(this);

        public override bool step(UnifyState state)
        {
            TraverseCountState* traverse = (TraverseCountState*)state.getTraverse(uniqid);
            if (!traverse->step()) return false;
            Varnode* vn = state.data(oldvarindex).getVarnode();
            state.data(newvarindex).setVarnode(vn);
            return true;
        }

        public override void collectTypes(List<UnifyDatatype> typelist)
        {
            typelist[oldvarindex] = UnifyDatatype(UnifyDatatype::var_type);
            typelist[newvarindex] = UnifyDatatype(UnifyDatatype::var_type);
        }

        public override int4 getBaseIndex() => oldvarindex;

        public override void print(TextWriter s, UnifyCPrinter printstate)
        {
            printstate.printIndent(s);
            s << printstate.getName(newvarindex) << " = " << printstate.getName(oldvarindex) << ';' << endl;
        }
    }
}
