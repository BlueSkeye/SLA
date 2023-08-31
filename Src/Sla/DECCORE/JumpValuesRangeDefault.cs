using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief A jump-table starting range with two possible execution paths
    ///
    /// This extends the basic JumpValuesRange having a single entry switch variable and
    /// adds a second entry point that takes only a single value. This value comes last in the iteration.
    internal class JumpValuesRangeDefault : JumpValuesRange
    {
        /// The extra value
        private ulong extravalue;
        /// The starting Varnode associated with the extra value
        private Varnode extravn;
        /// The starting PcodeOp associated with the extra value
        private PcodeOp extraop;
        /// \b true if the extra value has been visited by the iterator
        private /*mutable*/ bool lastvalue;

        /// Set the extra value explicitly
        public void setExtraValue(ulong val)
        {
            extravalue = val;
        }

        /// Set the associated start Varnode
        public void setDefaultVn(Varnode vn)
        {
            extravn = vn;
        }

        /// Set the associated start PcodeOp
        public void setDefaultOp(PcodeOp op)
        {
            extraop = op;
        }
        
        public override ulong getSize() => range.getSize() + 1;

        public override bool contains(ulong val)
        {
            if (extravalue == val)
                return true;
            return range.contains(val);
        }

        public override bool initializeForReading()
        {
            if (range.getSize() == 0)
            {
                curval = extravalue;
                lastvalue = true;
            }
            else
            {
                curval = range.getMin();
                lastvalue = false;
            }
            return true;
        }

        public override bool next()
        {
            if (lastvalue) return false;
            if (range.getNext(curval))
                return true;
            lastvalue = true;
            curval = extravalue;
            return true;
        }

        public override Varnode getStartVarnode() => lastvalue ? extravn : normqvn;

        public override PcodeOp getStartOp() => lastvalue ? extraop : startop;

        // The -extravalue- is not reversible
        public override bool isReversible() => !lastvalue;

        public override JumpValues clone()
        {
            JumpValuesRangeDefault res = new JumpValuesRangeDefault();
            res.range = range;
            res.normqvn = normqvn;
            res.startop = startop;
            res.extravalue = extravalue;
            res.extravn = extravn;
            res.extraop = extraop;
            return res;
        }
    }
}
