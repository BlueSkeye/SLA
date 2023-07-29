using Sla.CORE;
using Sla.DECCORE;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    internal class UnifyState
    {
        private ConstraintGroup container; // containing unifyer
        private List<UnifyDatatype> storemap = new List<UnifyDatatype>();
        private List<TraverseConstraint> traverselist = new List<TraverseConstraint>();
        private Funcdata fd;
        
        public UnifyState(ConstraintGroup uni)
        {
            container = uni;
            storemap.resize(container.getMaxNum() + 1, UnifyDatatype());
            container.collectTypes(storemap);
            container.buildTraverseState(*this);
        }

        ~UnifyState()
        {
            for (int4 i = 0; i < traverselist.size(); ++i)
                delete traverselist[i];
        }

        public int4 numTraverse() => traverselist.size();

        public void registerTraverseConstraint(TraverseConstraint t)
        {
            traverselist.push_back(t);
        }

        public UnifyDatatype data(int4 slot) => storemap[slot];

        public TraverseConstraint getTraverse(int4 slot) => traverselist[slot];

        public Funcdata getFunction() => fd;

        public OpBehavior getBehavior(OpCode opc)
        { // Get the behavior associated with a particular opcode
            Architecture* glb = fd.getArch();
            return glb.inst[opc].getBehavior();
        }

        public void setFunction(Funcdata f)
        {
            fd = f;
        }

        public void initialize(int4 id, Varnode vn)
        { // Enter an initial varnode (root) starting point
            storemap[id].setVarnode(vn);
        }

        public void initialize(int4 id, PcodeOp op)
        { // Enter an initial op (root) starting point
            storemap[id].setOp(op);
        }
    }
}
