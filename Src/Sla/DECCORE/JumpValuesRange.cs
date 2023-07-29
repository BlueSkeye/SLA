using ghidra;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief single entry switch variable that can take a range of values
    internal class JumpValuesRange : JumpValues
    {
        /// Acceptable range of values for the normalized switch variable
        protected CircleRange range;
        /// Varnode representing the normalized switch variable
        protected Varnode normqvn;
        /// First PcodeOp in the jump-table calculation
        protected PcodeOp startop;
        /// The current value pointed to be the iterator
        protected /*mutable*/ uintb curval;

        /// Set the range of values explicitly
        public void setRange(CircleRange rng)
        {
            range = rng;
        }

        /// Set the normalized switch Varnode explicitly
        public void setStartVn(Varnode vn)
        {
            normqvn = vn;
        }

        /// Set the starting PcodeOp explicitly
        public void setStartOp(PcodeOp op)
        {
            startop = op;
        }

        /// The starting value for the range and the step is preserved.  The
        /// ending value is set so there are exactly the given number of elements
        /// in the range.
        /// \param nm is the given number
        public override void truncate(int4 nm)
        {
            int4 rangeSize = 8 * sizeof(uintb) - count_leading_zeros(range.getMask());
            rangeSize >>= 3;
            uintb left = range.getMin();
            int4 step = range.getStep();
            uintb right = (left + step * nm) & range.getMask();
            range.setRange(left, right, rangeSize, step);
        }

        public override uintb getSize() => range.getSize();

        public override bool contains(uintb val) => range.contains(val);

        public override bool initializeForReading()
        {
            if (range.getSize() == 0) return false;
            curval = range.getMin();
            return true;
        }

        public override bool next() => range.getNext(curval);

        public override uintb getValue() => curval;

        public override Varnode getStartVarnode() => normqvn;

        public override PcodeOp getStartOp() => startop;

        public override bool isReversible() => true;

        public override JumpValues clone()
        {
            JumpValuesRange* res = new JumpValuesRange();
            res->range = range;
            res->normqvn = normqvn;
            res->startop = startop;
            return res;
        }
    }
}
