using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace ghidra
{
    /// \brief Helper class for resolving cross-references while deserializing BlockGraph objects
    /// FlowBlock objects are serialized with their associated \b index value and edges are serialized
    /// with the indices of the FlowBlock end-points.  During deserialization, this class maintains a
    /// list of FlowBlock objects sorted by index and then looks up the FlowBlock matching a given
    /// index as edges specify them.
    internal class BlockMap
    {
        /// The list of deserialized FlowBlock objects
        private List<FlowBlock> sortlist = new List<FlowBlock>();

        /// Construct a FlowBlock of the given type
        /// \param bt is the block_type
        /// \return a new instance of the specialized FlowBlock
        private FlowBlock? resolveBlock(FlowBlock.block_type bt)
        {
            switch (bt) {
                case FlowBlock.block_type.t_plain:
                    return new FlowBlock();
                case FlowBlock.block_type.t_copy:
                    return new BlockCopy(null);
                case FlowBlock.block_type.t_graph:
                    return new BlockGraph();
                default:
                    break;
            }
            return null;
        }

        /// Locate a FlowBlock with a given index
        /// Given a list of FlowBlock objects sorted by index, use binary search to find the FlowBlock with matching index
        /// \param list is the sorted list of FlowBlock objects
        /// \param ind is the FlowBlock index to match
        /// \return the matching FlowBlock or NULL
        private static FlowBlock? findBlock(List<FlowBlock> list, int ind)
        {
            int min = 0;
            int max = list.Count;
            max -= 1;
            while (min <= max) {
                int mid = (min + max) / 2;
                FlowBlock block = list[mid];
                if (block.getIndex() == ind)
                    return block;
                if (block.getIndex() < ind) {
                    min = mid + 1;
                }
                else {
                    max = mid - 1;
                }
            }
            return null;
        }

        /// Sort the list of FlowBlock objects
        public void sortList()
        {
            sortlist.Sort(FlowBlock.compareBlockIndex);
        }

        /// \brief Find the FlowBlock matching the given index
        /// \param index is the given index
        /// \return the FlowBlock matching the index
        public FlowBlock findLevelBlock(int index) 
        {
            return findBlock(sortlist, index);
        }

        /// Create a FlowBlock of the named type
        /// Given the name of a block (deserialized from a \<bhead> tag), build the corresponding type of block.
        /// \param name is the name of the block type
        /// \return a new instance of the named FlowBlock type
        public FlowBlock? createBlock(string name)
        {
            FlowBlock.block_type bt = FlowBlock.nameToType(name);
            FlowBlock? bl = resolveBlock(bt);
            sortlist.Add(bl);
            return bl;
        }
    }
}
