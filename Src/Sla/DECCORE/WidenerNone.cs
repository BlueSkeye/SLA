using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief Class for freezing value sets at a specific iteration (to accelerate convergence)
    ///
    /// The value sets don't reach a true stable state but instead lock in a description of the
    /// first few values that \e reach a given Varnode. The ValueSetSolver does normal iteration,
    /// but individual ValueSets \e freeze after a specific number of iterations (3 by default),
    /// instead of growing to a true stable state. This gives evidence of iteration in the underlying
    /// code, showing the initial value and frequently the step size.
    internal class WidenerNone : Widener
    {
        /// The iteration at which all change ceases
        private int freezeIteration;
        
        public WidenerNone()
        {
            freezeIteration = 3;
        }
        
        public override int determineIterationReset(ValueSet valueSet)
        {
            if (valueSet.getCount() >= freezeIteration)
                return freezeIteration; // Reset to point just after any widening
            return valueSet.getCount();
        }

        public override bool checkFreeze(ValueSet valueSet)
        {
            if (valueSet.getRange().isFull())
                return true;
            return (valueSet.getCount() >= freezeIteration);
        }

        public override bool doWidening(ValueSet valueSet,CircleRange range, CircleRange newRange)
        {
            range = newRange;
            return true;
        }
    }
}
