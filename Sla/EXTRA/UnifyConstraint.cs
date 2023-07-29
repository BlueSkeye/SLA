using Sla.EXTRA;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace Sla.EXTRA
{
    internal abstract class UnifyConstraint
    {
        // friend class ConstraintGroup;
        // Unique identifier for constraint for retrieving state
        protected int4 uniqid;
        protected int4 maxnum;

        protected UnifyConstraint copyid(UnifyConstraint op)
        {
            uniqid = op->uniqid;
            maxnum = op->maxnum;
            return this;
        }

        ~UnifyConstraint()
        {
        }

        public int4 getId() => uniqid;

        public int4 getMaxNum() => maxnum;

        public abstract UnifyConstraint clone();

        public override void initialize(UnifyState state)
        {
            // Default initialization (with only 1 state)
            TraverseCountState* traverse = (TraverseCountState*)state.getTraverse(uniqid);
            traverse->initialize(1);    // Initialize with only one state
        }

        public abstract bool step(UnifyState state)
        {
            TraverseCountState* traverse = (TraverseCountState*)state.getTraverse(uniqid);
            if (!traverse->step()) return false;
            uintb ourconst = expr->getConstant(state);
            if (istrue)
                return (ourconst != 0);
            return (ourconst == 0);
        }

        public override void buildTraverseState(UnifyState state)
        {
            // Build the default boolean traversal state
            if (uniqid != state.numTraverse())
                throw LowlevelError("Traverse id does not match index");
            TraverseConstraint* newt = new TraverseCountState(uniqid);
            state.registerTraverseConstraint(newt);
        }

        public override void setId(int4 id)
        {
            uniqid = id;
            id += 1;
        }

        public override void collectTypes(List<UnifyDatatype> typelist)
        {
        }

        public override int4 getBaseIndex() => -1;

        public abstract void print(TextWriter s, UnifyCPrinter printstate);

        public override bool isDummy() => false;

        public override void removeDummy()
        {
        }
    }
}
