using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief A class that holds a data-type traversal state during type propagation
    ///
    /// For a given Varnode, this class iterates all the possible edges its
    /// data-type might propagate through.
    internal class PropagationState
    {
        /// The root Varnode
        public Varnode vn;
        /// Iterator to current descendant being enumerated
        public IEnumerator<PcodeOp> iter;
        /// The current descendant or the defining PcodeOp
        public PcodeOp? op;
        /// Slot holding Varnode for descendant PcodeOp
        public int inslot;
        /// Current edge relative to current PcodeOp
        public int slot;

        /// \param v is the root Varnode to iterate over
        public PropagationState(Varnode v)
        {
            vn = v;
            iter = vn->beginDescend();
            if (iter != vn->endDescend())
            {
                op = *iter++;
                if (op->getOut() != (Varnode*)0)
                    slot = -1;
                else
                    slot = 0;
                inslot = op->getSlot(vn);
            }
            else
            {
                op = vn->getDef();
                inslot = -1;
                slot = 0;
            }
        }

        /// Advance to the next propagation edge
        /// At the high level, this iterates through all the descendant
        /// PcodeOps of the root Varnode, then the defining PcodeOp.
        /// At the low level, this iterates from the output Varnode
        /// of the current PcodeOp then through all the input Varnodes
        public void step()
        {
            slot += 1;
            if (slot < op->numInput())
                return;
            if (iter != vn->endDescend())
            {
                op = *iter++;
                if (op->getOut() != (Varnode*)0)
                    slot = -1;
                else
                    slot = 0;
                inslot = op->getSlot(vn);
                return;
            }
            if (inslot == -1)
                op = (PcodeOp*)0;
            else
                op = vn->getDef();
            inslot = -1;
            slot = 0;
        }

        /// Return \b true if there are edges left to iterate
        public bool valid() => (op != null);
    }
}
