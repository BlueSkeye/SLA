using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    // For a ConstraintOr, exactly one subconstraint needs to be true, for the whole constraint to match
    // The constraints are tested sequentially, but there can be no dependency between subconstraints
    internal class ConstraintOr : ConstraintGroup
    {
        public override UnifyConstraint clone()
        {
            ConstraintOr* res = new ConstraintOr();
            for (int i = 0; i < constraintlist.size(); ++i)
            {
                UnifyConstraint* subconst = constraintlist[i].clone();
                res.constraintlist.Add(subconst);
            }
            res.copyid(this);
            return res;
        }

        public override void initialize(UnifyState state)
        {
            TraverseCountState* traverse = (TraverseCountState*)state.getTraverse(uniqid);
            traverse.initialize(constraintlist.size());
        }

        public override bool step(UnifyState state)
        {
            TraverseCountState* traverse = (TraverseCountState*)state.getTraverse(uniqid);
            int stateind = traverse.getState();
            UnifyConstraint* cur;
            if (stateind == -1)
            { // First time through
                if (!traverse.step()) return false;
                stateind = traverse.getState();
                cur = getConstraint(stateind);
                cur.initialize(state);
            }
            else
                cur = getConstraint(stateind);
            for (; ; )
            {
                if (cur.step(state)) return true;
                if (!traverse.step()) break;
                stateind = traverse.getState();
                cur = getConstraint(stateind);
                cur.initialize(state);
            }
            return false;
        }

        public override void buildTraverseState(UnifyState state)
        {
            if (uniqid != state.numTraverse())
                throw new LowlevelError("Traverse id does not match index in or");
            TraverseCountState* trav = new TraverseCountState(uniqid);
            state.registerTraverseConstraint(trav);

            for (int i = 0; i < constraintlist.size(); ++i)
            {
                UnifyConstraint* subconstraint = constraintlist[i];
                subconstraint.buildTraverseState(state);
            }
        }

        // Does not have a base
        public override int getBaseIndex() => -1;

        public override void print(TextWriter s, UnifyCPrinter printstate)
        {
            printstate.printIndent(s);
            s << "for(i" << dec << printstate.getDepth() << "=0;i" << printstate.getDepth() << '<';
            s << (int)constraintlist.size() << ";++i" << printstate.getDepth() << ") {" << endl;
            printstate.incDepth();  // permanent increase in depth
            for (int i = 0; i < constraintlist.size(); ++i)
            {
                printstate.printIndent(s);
                if (i != 0)
                    s << "else ";
                if (i != constraintlist.size() - 1)
                    s << "if (i" << printstate.getDepth() - 1 << " == " << dec << i << ") ";
                s << '{' << endl;
                int olddepth = printstate.getDepth();
                printstate.incDepth();
                constraintlist[i].print(s, printstate);
                printstate.popDepth(s, olddepth);
            }
        }
    }
}
