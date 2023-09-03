
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
        private ulong typeId;
        /// Id of PcodeOp edge
        private uint opTime;
        /// Encoding of the slot and pointer-ness
        private int encoding;

        /// Construct from components
        /// \param parent is a parent data-type that needs to be resolved
        /// \param op is the PcodeOp reading/writing the \b parent data-type
        /// \param slot is the slot (>=0 for input, -1 for output) accessing the \b parent
        public ResolveEdge(Datatype parent, PcodeOp op, int slot)
        {
            opTime = op.getTime();
            encoding = slot;
            if (parent.getMetatype() == type_metatype.TYPE_PTR) {
                // Strip pointer
                typeId = ((TypePointer)parent).getPtrTo().getId();
                // Encode the fact that a pointer is getting accessed
                encoding += 0x1000;
            }
            else if (parent.getMetatype() == type_metatype.TYPE_PARTIALUNION)
                typeId = ((TypePartialUnion)parent).getParentUnion().getId();
            else
                typeId = parent.getId();
        }

        /// Compare two edges
        /// Compare based on the data-type, the \b slot, and the PcodeOp's unique id.
        /// \param op2 is the other edge to compare with \b this
        /// \return \b true if \b this should be ordered before the other edge
        public static bool operator <(ResolveEdge op1, ResolveEdge op2)
        {
            if (op1.typeId != op2.typeId)
                return (op1.typeId < op2.typeId);
            if (op1.encoding != op2.encoding)
                return (op1.encoding < op2.encoding);
            return (op1.opTime < op2.opTime);
        }

        public static bool operator >(ResolveEdge op1, ResolveEdge op2)
        {
            if (op1.typeId != op2.typeId)
                return (op1.typeId > op2.typeId);
            if (op1.encoding != op2.encoding)
                return (op1.encoding > op2.encoding);
            return (op1.opTime > op2.opTime);
        }
    }
}
