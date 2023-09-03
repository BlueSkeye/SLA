using Sla.CORE;

namespace Sla.EXTRA
{
    internal abstract class UnifyConstraint
    {
        // friend class ConstraintGroup;
        // Unique identifier for constraint for retrieving state
        protected int uniqid;
        protected int maxnum;

        protected UnifyConstraint copyid(UnifyConstraint op)
        {
            uniqid = op.uniqid;
            maxnum = op.maxnum;
            return this;
        }

        ~UnifyConstraint()
        {
        }

        public int getId() => uniqid;

        public int getMaxNum() => maxnum;

        public abstract UnifyConstraint clone();

        public virtual void initialize(UnifyState state)
        {
            // Default initialization (with only 1 state)
            TraverseCountState traverse = (TraverseCountState)state.getTraverse(uniqid);
            // Initialize with only one state
            traverse.initialize(1);
        }

        public virtual bool step(UnifyState state)
        {
            TraverseCountState traverse = (TraverseCountState)state.getTraverse(uniqid);
            if (!traverse.step()) return false;
            ulong ourconst = expr.getConstant(state);
            if (istrue)
                return (ourconst != 0);
            return (ourconst == 0);
        }

        public virtual void buildTraverseState(UnifyState state)
        {
            // Build the default boolean traversal state
            if (uniqid != state.numTraverse())
                throw new LowlevelError("Traverse id does not match index");
            TraverseConstraint newt = new TraverseCountState(uniqid);
            state.registerTraverseConstraint(newt);
        }

        public virtual void setId(int id)
        {
            uniqid = id;
            id += 1;
        }

        public virtual void collectTypes(List<UnifyDatatype> typelist)
        {
        }

        public virtual int getBaseIndex() => -1;

        public abstract void print(TextWriter s, UnifyCPrinter printstate);

        public virtual bool isDummy() => false;

        public virtual void removeDummy()
        {
        }
    }
}
