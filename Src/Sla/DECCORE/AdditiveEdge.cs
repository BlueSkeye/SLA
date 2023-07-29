using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// Class representing a \e term in an additive expression
    internal class AdditiveEdge
    {
        /// Lone descendant reading the term
        private PcodeOp op;
        /// The input slot of the term
        private int slot;
        /// The term Varnode
        private Varnode vn;
        /// The (optional) multiplier being applied to the term
        private PcodeOp mult;
        
        public AdditiveEdge(PcodeOp o, int s, PcodeOp m)
        {
            op = o;
            slot = s;
            vn = op.getIn(slot);
            mult = m;
        }

        /// Get the multiplier PcodeOp
        public PcodeOp getMultiplier() => mult;

        /// Get the component PcodeOp adding in the term
        public PcodeOp getOp() => op;

        /// Get the slot reading the term
        public int getSlot() => slot;

        /// Get the Varnode term
        public Varnode getVarnode() => vn;
    }
}
