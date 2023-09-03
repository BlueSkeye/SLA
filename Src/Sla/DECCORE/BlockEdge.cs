using Sla.CORE;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static Sla.DECCORE.FlowBlock;

namespace Sla.DECCORE
{
    /// \brief A control-flow edge between blocks (FlowBlock)
    /// The edge is owned by the source block and can have FlowBlock::edge_flags
    /// labels applied to it.  The \b point indicates the FlowBlock at the other end
    /// from the source block. NOTE: The control-flow direction of the edge can
    /// only be determined from context, whether the edge is in the incoming or outgoing edge list.
    internal class BlockEdge
    {
        /// Label of the edge
        internal edge_flags label;
        /// Other end of the edge
        internal FlowBlock point;
        /// Index for edge coming other way
        internal int reverse_index;

        /// Constructor for use with decode
        internal BlockEdge()
        {
        }

        /// Constructor
        internal BlockEdge(FlowBlock pt, edge_flags lab, int rev)
        {
            label = lab;
            point = pt;
            reverse_index = rev;
        }

        /// Encode \b this edge to a stream
        /// The edge is saved assuming we already know what block we are @in.
        /// \param encoder is the stream encoder
        internal void encode(Sla.CORE.Encoder encoder)
        {
            encoder.openElement(ElementId.ELEM_EDGE);
            // We are not saving label currently
            // Reference to other end of edge
            encoder.writeSignedInteger(AttributeId.ATTRIB_END, point.getIndex());
            // Position within other blocks edgelist
            encoder.writeSignedInteger(AttributeId.ATTRIB_REV, reverse_index);
            encoder.closeElement(ElementId.ELEM_EDGE);
        }

        /// Restore \b this edge from a stream
        /// Parse an \<edge> element
        /// \param decoder is the stream decoder
        /// \param resolver is used to cross-reference the edge's FlowBlock endpoints
        internal void decode(Sla.CORE.Decoder decoder, BlockMap resolver)
        {
            uint elemId = decoder.openElement(ElementId.ELEM_EDGE);
            // Tag does not currently contain info about label
            label = 0;
            int endIndex = (int)decoder.readSignedInteger(AttributeId.ATTRIB_END);
            point = resolver.findLevelBlock(endIndex);
            if (null == point) {
                throw new LowlevelError("Bad serialized edge in block graph");
            }
            reverse_index = (int)decoder.readSignedInteger(AttributeId.ATTRIB_REV);
            decoder.closeElement(elemId);
        }
    }
}
