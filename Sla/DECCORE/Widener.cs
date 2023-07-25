using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief Class holding a particular widening strategy for the ValueSetSolver iteration algorithm
    ///
    /// This obects gets to decide when a value set gets \e frozen (checkFreeze()), meaning the set
    /// doesn't change for the remaining iteration steps. It also gets to decide when and by how much
    /// value sets get artificially increased in size to accelerate reaching their stable state (doWidening()).
    internal abstract class Widener
    {
        ~Widener()
        {
        }

        /// \brief Upon entering a fresh partition, determine how the given ValueSet count should be reset
        ///
        /// \param valueSet is the given value set
        /// \return the value of the iteration counter to reset to
        public abstract int4 determineIterationReset(ValueSet valueSet);

        /// \brief Check if the given value set has been frozen for the remainder of the iteration process
        ///
        /// \param valueSet is the given value set
        /// \return \b true if the valueSet will no longer change
        public abstract bool checkFreeze(ValueSet valueSet);

        /// \brief For an iteration that isn't stabilizing attempt to widen the given ValueSet
        ///
        /// Change the given range based on its previous iteration so that it stabilizes more
        /// rapidly on future iterations.
        /// \param valueSet is the given value set
        /// \param range is the previous form of the given range (and storage for the widening result)
        /// \param newRange is the current iteration of the given range
        /// \return \b true if widening succeeded
        public abstract bool doWidening(ValueSet valueSet,CircleRange range, CircleRange newRange);
    }
}
