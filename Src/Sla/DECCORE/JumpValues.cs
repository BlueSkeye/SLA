using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief An iterator over values a switch variable can take
    ///
    /// This iterator is intended to provide the start value for emulation
    /// of a jump-table model to obtain the associated jump-table destination.
    /// Each value can be associated with a starting Varnode and PcodeOp in
    /// the function being emulated, via getStartVarnode() and getStartOp().
    internal abstract class JumpValues
    {
        ~JumpValues()
        {
        }

        /// Truncate the number of values to the given number
        public abstract void truncate(int4 nm);

        /// Return the number of values the variables can take
        public abstract uintb getSize();

        /// Return \b true if the given value is in the set of possible values
        public abstract bool contains(uintb val);

        /// \brief Initialize \b this for iterating over the set of possible values
        ///
        /// \return \b true if there are any values to iterate over
        public abstract bool initializeForReading();

        /// Advance the iterator, return \b true if there is another value
        public abstract bool next();

        /// Get the current value
        public abstract uintb getValue();

        /// Get the Varnode associated with the current value
        public abstract Varnode getStartVarnode();

        /// Get the PcodeOp associated with the current value
        public abstract PcodeOp getStartOp();

        /// Return \b true if the current value can be reversed to get a label
        public abstract bool isReversible();

        /// Clone \b this iterator
        public abstract JumpValues clone();
    }
}
