using Sla.CORE;
using Sla.EXTRA;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    // For a ConstraintGroup, the list of subconstraints must all match for the whole constraint to match.
    // Constraints are tested first to last, i.e. testing for constraint n can assume that 1 thru n-1 match.
    internal class ConstraintGroup : UnifyConstraint
    {
        protected List<UnifyConstraint> constraintlist = new List<UnifyConstraint>();
        
        public ConstraintGroup()
        {
            maxnum = -1;
        }

        ~ConstraintGroup()
        {
            //for (uint i = 0; i < constraintlist.size(); ++i)
            //    delete constraintlist[i];
            // We do not own the traverselist objects
        }

        public UnifyConstraint getConstraint(int slot) => constraintlist[slot];

        public void addConstraint(UnifyConstraint a)
        {
            constraintlist.Add(a);

            if (a.getMaxNum() > maxnum)
                maxnum = a.getMaxNum();
        }

        public int numConstraints() => constraintlist.size();

        public void deleteConstraint(int slot)
        {
            constraintlist.RemoveAt(slot);
            // delete mydel;
        }

        public void mergeIn(ConstraintGroup b)
        {
            // Merge all the subconstraints from -b- into this
            for (int i = 0; i < b.constraintlist.size(); ++i)
                addConstraint(b.constraintlist[i]);
            b.constraintlist.Clear();  // Constraints are no longer controlled by -b-
            // delete b;
        }

        public override UnifyConstraint clone()
        {
            ConstraintGroup res = new ConstraintGroup();
            for (int i = 0; i < constraintlist.size(); ++i) {
                UnifyConstraint subconst = constraintlist[i].clone();
                res.constraintlist.Add(subconst);
            }
            res.copyid(this);
            return res;
        }

        public override void initialize(UnifyState state)
        {
            TraverseGroupState traverse = (TraverseGroupState)state.getTraverse(uniqid);
            traverse.setState(-1);
        }

        public override bool step(UnifyState state)
        {
            TraverseGroupState traverse = (TraverseGroupState)state.getTraverse(uniqid);

            UnifyConstraint subconstraint;
            TraverseConstraint subtraverse;
            int subindex;
            int stateint;
            int max = constraintlist.size();
            do {
                stateint = traverse.getState();
                if (stateint == 0) {
                    // Attempt a step at current constraint
                    subindex = traverse.getCurrentIndex();
                    subtraverse = traverse.getSubTraverse(subindex);
                    subconstraint = constraintlist[subindex];
                    if (subconstraint.step(state)) {
                        traverse.setState(1);  // Now try a push
                        subindex += 1;
                        traverse.setCurrentIndex(subindex);
                    }
                    else {
                        subindex -= 1;
                        if (subindex < 0) return false; // Popped off the top
                        traverse.setCurrentIndex(subindex);
                        traverse.setState(0);  // Try a step next
                    }
                }
                else if (stateint == 1) {
                    // Push
                    subindex = traverse.getCurrentIndex();
                    subtraverse = traverse.getSubTraverse(subindex);
                    subconstraint = constraintlist[subindex];
                    subconstraint.initialize(state);
                    traverse.setState(0);  // Try a step next
                }
                else {
                    // Very first time through
                    traverse.setCurrentIndex(0);
                    subindex = 0;
                    subtraverse = traverse.getSubTraverse(subindex);
                    subconstraint = constraintlist[subindex];
                    subconstraint.initialize(state);   // Initialize the very first subcontraint
                    traverse.setState(0);  // Now try a step
                }
            } while (subindex < max);
            subindex -= 1;
            traverse.setCurrentIndex(subindex);
            traverse.setState(0);  // Have full solution, do step next, to get to next solution
            return true;
        }

        public override void collectTypes(List<UnifyDatatype> typelist)
        {
            for (int i = 0; i < constraintlist.size(); ++i)
                constraintlist[i].collectTypes(typelist);
        }

        public override void buildTraverseState(UnifyState state)
        {
            if (uniqid != state.numTraverse())
                throw new LowlevelError("Traverse id does not match index");
            TraverseGroupState basetrav = new TraverseGroupState(uniqid);
            state.registerTraverseConstraint(basetrav);

            for (int i = 0; i < constraintlist.size(); ++i) {
                UnifyConstraint subconstraint = constraintlist[i];
                subconstraint.buildTraverseState(state);
                TraverseConstraint subtraverse = state.getTraverse(subconstraint.getId());
                basetrav.addTraverse(subtraverse);
            }
        }

        public override void setId(int id)
        {
            base.setId(id);
            for (int i = 0; i < constraintlist.size(); ++i)
                constraintlist[i].setId(id);
        }

        public override int getBaseIndex() => constraintlist.GetLastItem().getBaseIndex();

        public override void print(TextWriter s, UnifyCPrinter printstate)
        {
            for (int i = 0; i < constraintlist.size(); ++i)
                constraintlist[i].print(s, printstate);
        }

        public override void removeDummy()
        {
            // Remove any dummy constraints within us
            List<UnifyConstraint> newlist = new List<UnifyConstraint>();

            for (int i = 0; i < constraintlist.size(); ++i) {
                UnifyConstraint cur = constraintlist[i];
                if (cur.isDummy()) {
                    // delete cur;
                }
                else {
                    cur.removeDummy();
                    newlist.Add(cur);
                }
            }
            constraintlist = newlist;
        }
    }
}
