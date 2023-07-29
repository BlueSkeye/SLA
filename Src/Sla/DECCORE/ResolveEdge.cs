using ghidra;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief A data-flow edge to which a resolved data-type can be assigned
    ///
    /// The edge is associated with the specific data-type that needs to be resolved,
    /// which is typically a union or a pointer to a union.  The edge collapses different
    /// kinds of pointers to the same base union.
    internal class ResolveEdge
    {
        /// Id of base data-type being resolved
        private uint8 typeId;
        /// Id of PcodeOp edge
        private uintm opTime;
        /// Encoding of the slot and pointer-ness
        private int4 encoding;

        /// Construct from components
        /// \param parent is a parent data-type that needs to be resolved
        /// \param op is the PcodeOp reading/writing the \b parent data-type
        /// \param slot is the slot (>=0 for input, -1 for output) accessing the \b parent
        public ResolveEdge(Datatype parent, PcodeOp op, int4 slot)
        {
            opTime = op.getTime();
            encoding = slot;
            if (parent.getMetatype() == TYPE_PTR)
            {
                typeId = ((TypePointer*)parent).getPtrTo().getId();   // Strip pointer
                encoding += 0x1000;     // Encode the fact that a pointer is getting accessed
            }
            else if (parent.getMetatype() == TYPE_PARTIALUNION)
                typeId = ((TypePartialUnion*)parent).getParentUnion().getId();
            else
                typeId = parent.getId();
        }

        /// Compare two edges
        public static bool operator <(ResolveEdge op1, ResolveEdge op2);
    }
}
