using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief Class for doing normal widening
    ///
    /// Widening is attempted at a specific iteration. If a landmark is available, it is used
    /// to do a controlled widening, holding the stable range boundary constant. Otherwise a
    /// full range is produced.  At a later iteration, a full range is produced automatically.
    internal class WidenerFull :  Widener
    {
        /// The iteration at which widening is attempted
        private int4 widenIteration;
        /// The iteration at which a full range is produced
        private int4 fullIteration;

        /// Constructor with default iterations
        public WidenerFull()
        {
            widenIteration = 2;
            fullIteration = 5;
        }

        /// Constructor specifying iterations
        public WidenerFull(int4 wide, int4 full)
        {
            widenIteration = wide;
            fullIteration = full;
        }

        public override int4 determineIterationReset(ValueSet valueSet)
        {
            if (valueSet.getCount() >= widenIteration)
                return widenIteration;  // Reset to point just after any widening
            return 0;           // Delay widening, if we haven't performed it yet
        }

        public override bool checkFreeze(ValueSet valueSet)
        {
            return valueSet.getRange().isFull();
        }

        public override bool doWidening(ValueSet valueSet, CircleRange range, CircleRange newRange)
        {
            if (valueSet.getCount() < widenIteration)
            {
                range = newRange;
                return true;
            }
            else if (valueSet.getCount() == widenIteration)
            {
                CircleRange landmark = valueSet.getLandMark();
                if (landmark != (CircleRange*)0) {
                    bool leftIsStable = range.getMin() == newRange.getMin();
                    range = newRange;   // Preserve any new step information
                    if (landmark->contains(range))
                    {
                        range.widen(*landmark, leftIsStable);
                        return true;
                    }
                    else
                    {
                        CircleRange constraint = *landmark;
                        constraint.invert();
                        if (constraint.contains(range))
                        {
                            range.widen(constraint, leftIsStable);
                            return true;
                        }
                    }
                }
            }
            else if (valueSet.getCount() < fullIteration)
            {
                range = newRange;
                return true;
            }
            return false;       // Indicate that constrained widening failed (set to full)
        }
    }
}
