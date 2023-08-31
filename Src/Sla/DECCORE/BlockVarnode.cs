using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief Helper class associating a Varnode with the block where it is defined
    ///
    /// This class explicitly stores a Varnode with the index of the BlockBasic that defines it.
    /// If a Varnode does not have a defining PcodeOp it is assigned an index of 0.
    /// This facilitates quicker sorting of Varnodes based on their defining block.
    internal class BlockVarnode
    {
        /// Index of BlockBasic defining Varnode
        private int index;
        /// The Varnode itself
        private Varnode vn;

        /// Set \b this as representing the given Varnode
        /// This instance assumes the identity of the given Varnode and the defining index is
        /// cached to facilitate quick sorting.
        /// \param v is the given Varnode
        public void set(Varnode v)
        {
            vn = v;
            PcodeOp op = vn.getDef();

            if (op == (PcodeOp)null)
                index = 0;
            else
                index = op.getParent().getIndex();
        }

        public static bool operator <(BlockVarnode op1, BlockVarnode op2)
        {
            return (op1.index<op2.index);
        }

        /// Get the Varnode represented by \b this
        public Varnode getVarnode() => vn;

        /// Get the Varnode's defining block index
        public int getIndex() => index;

        /// \brief Find the first Varnode defined in the BlockBasic of the given index
        ///
        /// A BlockVarnode is identified from a sorted \b list. The position of the first BlockVarnode
        /// in this list that has the given BlockBasic \e index is returned.
        /// \param blocknum is the index of the BlockBasic to search for
        /// \param list is the sorted list of BlockVarnodes
        /// \return the index of the BlockVarnode within the list or -1 if no Varnode in the block is found
        public static int findFront(int blocknum, List<BlockVarnode> list)
        {
            int min = 0;
            int max = list.size() - 1;
            while (min < max)
            {
                int cur = (min + max) / 2;
                int curblock = list[cur].getIndex();
                if (curblock >= blocknum)
                    max = cur;
                else
                    min = cur + 1;
            }
            if (min > max)
                return -1;
            if (list[min].getIndex() != blocknum)
                return -1;
            return min;
        }
    }
}
