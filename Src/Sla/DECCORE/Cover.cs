using Sla.DECCORE;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief A description of the topological scope of a single variable object
    ///
    /// The \b topological \b scope of a variable within a function is the set of
    /// locations within the code of the function where that variable holds a variable.
    /// For the decompiler, a high-level variable in this sense, HighVariable, is a collection
    /// of Varnode objects.  In order to merge Varnodes into a HighVariable, the topological
    /// scope of each Varnode must not intersect because that would mean the high-level variable
    /// holds different values at the same point in the function.
    ///
    /// Internally this is implemented as a map from basic block to their non-empty CoverBlock
    internal class Cover
    {
        /// block index . CoverBlock
        private Dictionary<int, CoverBlock> cover;
        
        /// Global empty CoverBlock for blocks not covered by \b this
        internal static readonly CoverBlock emptyBlock = new CoverBlock();

        /// Fill-in \b this recursively from the given block
        /// Add to \b this Cover recursively, starting at bottom of the given block
        /// and filling in backward until we run into existing cover.
        /// \param bl is the starting block to add
        private void addRefRecurse(FlowBlock bl)
        {
            int j;
            uint ustart, ustop;

            CoverBlock & block(cover[bl.getIndex()]);
            if (block.empty())
            {
                block.setAll();     // No cover encountered, fill in entire block
                                    //    if (bl.InSize()==0)
                                    //      throw new LowlevelError("Ref point is not in flow of defpoint");
                for (j = 0; j < bl.sizeIn(); ++j)  // Recurse to all blocks that fall into bl
                    addRefRecurse(bl.getIn(j));
            }
            else
            {
                PcodeOp* op = block.getStop();
                ustart = CoverBlock::getUIndex(block.getStart());
                ustop = CoverBlock::getUIndex(op);
                if ((ustop != uint.MaxValue) && (ustop >= ustart))
                    block.setEnd((PcodeOp*)1); // Fill in to the bottom


                if ((ustop == (uint)0) && (block.getStart() == (PcodeOp)null)) {
                    if ((op != (PcodeOp)null)&& (op.code() == CPUI_MULTIEQUAL)) {
                        // This block contains only an infinitesimal tip
                        // of cover through one branch of a MULTIEQUAL
                        // we still need to traverse through branches
                        for (j = 0; j < bl.sizeIn(); ++j)
                            addRefRecurse(bl.getIn(j));
                    }
                }


            }
        }

        /// Clear \b this to an empty Cover
        public void clear()
        {
            cover.Clear();
        }

        /// Give ordering of \b this and another Cover
        /// Compare \b this with another Cover by comparing just
        /// the indices of the first blocks respectively that are partly covered.
        /// Return -1, 0, or 1 if \b this Cover's first block has a
        /// smaller, equal, or bigger index than the other Cover's first block.
        /// \param op2 is the other Cover
        /// \return the comparison value
        public int compareTo(Cover op2)
        {
            int a, b;

            Dictionary<int, CoverBlock>::const_iterator iter;
            iter = cover.begin();
            if (iter == cover.end())
                a = 1000000;
            else
                a = (*iter).first;
            iter = op2.cover.begin();
            if (iter == op2.cover.end())
                b = 1000000;
            else
                b = (*iter).first;

            if (a < b)
            {
                return -1;
            }
            else if (a == b)
            {
                return 0;
            }
            return 1;
        }

        /// Get the CoverBlock corresponding to the i-th block
        /// Return a representative CoverBlock describing how much of the given block
        /// is covered by \b this
        /// \param i is the index of the given block
        /// \return a reference to the corresponding CoverBlock
        public CoverBlock getCoverBlock(int i)
        {
            Dictionary<int, CoverBlock>::const_iterator iter = cover.find(i);
            if (iter == cover.end())
                return emptyBlock;
            return (*iter).second;
        }

        /// Characterize the intersection between \b this and another Cover.
        /// Return
        ///   - 0 if there is no intersection
        ///   - 1 if the only intersection is on a boundary point
        ///   - 2 if the intersection contains a range of p-code ops
        ///
        /// \param op2 is the other Cover
        /// \return the intersection characterization
        public int intersect(Cover op2)
        {
            Dictionary<int, CoverBlock>::const_iterator iter, iter2;
            int res, newres;

            res = 0;
            iter = cover.begin();
            iter2 = op2.cover.begin();

            for (; ; )
            {
                if (iter == cover.end()) return res;
                if (iter2 == op2.cover.end()) return res;

                if ((*iter).first < (*iter2).first)
                    ++iter;
                else if ((*iter).first > (*iter2).first)
                    ++iter2;
                else
                {
                    newres = (*iter).second.intersect((*iter2).second);
                    if (newres == 2) return 2;
                    if (newres == 1)
                        res = 1;        // At least a point intersection
                    ++iter;
                    ++iter2;
                }
            }
            return res;
        }

        /// Characterize the intersection on a specific block
        /// Looking only at the given block, Return
        ///   - 0 if there is no intersection
        ///   - 1 if the only intersection is on a boundary point
        ///   - 2 if the intersection contains a range of p-code ops
        ///
        /// \param blk is the index of the given block
        /// \param op2 is the other Cover
        /// \return the characterization
        public int intersectByBlock(int blk, Cover op2)
        {
            Dictionary<int, CoverBlock>::const_iterator iter;

            iter = cover.find(blk);
            if (iter == cover.end()) return 0;

            Dictionary<int, CoverBlock>::const_iterator iter2;

            iter2 = op2.cover.find(blk);
            if (iter2 == op2.cover.end()) return 0;

            return (*iter).second.intersect((*iter2).second);
        }

        /// \brief Generate a list of blocks that intersect
        ///
        /// For each block for which \b this and another Cover intersect,
        /// and the block's index to a result list if the type of intersection
        /// exceeds a characterization level.
        /// \param listout will hold the list of intersecting block indices
        /// \param op2 is the other Cover
        /// \param level is the characterization threshold which must be exceeded
        public void intersectList(List<int> listout, Cover op2, int level)
        {
            Dictionary<int, CoverBlock>::const_iterator iter, iter2;
            int val;

            listout.clear();

            iter = cover.begin();
            iter2 = op2.cover.begin();

            for (; ; )
            {
                if (iter == cover.end()) return;
                if (iter2 == op2.cover.end()) return;

                if ((*iter).first < (*iter2).first)
                    ++iter;
                else if ((*iter).first > (*iter2).first)
                    ++iter2;
                else
                {
                    val = (*iter).second.intersect((*iter2).second);
                    if (val >= level)
                        listout.Add((*iter).first);
                    ++iter;
                    ++iter2;
                }
            }
        }

        /// \brief Does \b this contain the given PcodeOp
        ///
        /// \param op is the given PcodeOp
        /// \param max is 1 to test for any containment, 2 to force interior containment
        /// \return true if there is containment
        public bool contain(PcodeOp op, int max)
        {
            Dictionary<int, CoverBlock>::const_iterator iter;

            iter = cover.find(op.getParent().getIndex());
            if (iter == cover.end()) return false;
            if ((*iter).second.contain(op))
            {
                if (max == 1) return true;
                if (0 == (*iter).second.boundary(op)) return true;
            }
            return false;
        }

        /// \brief Check the definition of a Varnode for containment
        ///
        /// If the given Varnode has a defining PcodeOp this is
        /// checked for containment.  If the Varnode is an input,
        /// check if \b this covers the start of the function.
        ///
        /// Return:
        ///   - 0 if cover does not contain varnode definition
        ///   - 1 if there if it is contained in interior
        ///   - 2 if the defining points intersect
        ///   - 3 if Cover's tail is the varnode definition
        ///
        /// \param vn is the given Varnode
        /// \return the containment characterization
        public int containVarnodeDef(Varnode vn)
        {
            PcodeOp? op = vn.getDef();
            int blk;

            if (op == null) {
                op = (PcodeOp*)2;
                blk = 0;
            }
            else
                blk = op.getParent().getIndex();
            Dictionary<int, CoverBlock>::const_iterator iter = cover.find(blk);
            if (iter == cover.end()) return 0;
            if ((*iter).second.contain(op))
            {
                int boundtype = (*iter).second.boundary(op);
                if (boundtype == 0) return 1;
                if (boundtype == 2) return 2;
                return 3;
            }
            return 0;
        }

        /// Merge \b this with another Cover block by block
        /// \param op2 is the other Cover
        public void merge(Cover op2)
        {
            Dictionary<int, CoverBlock>::const_iterator iter;

            for (iter = op2.cover.begin(); iter != op2.cover.end(); ++iter)
                cover[(*iter).first].merge((*iter).second);
        }

        /// Reset \b this based on def-use of a single Varnode
        /// The cover is set to all p-code ops between the point where
        /// the Varnode is defined and all the points where it is read
        /// \param vn is the single Varnode
        public void rebuild(Varnode vn)
        {
            list<PcodeOp*>::const_iterator iter;

            addDefPoint(vn);
            for (iter = vn.beginDescend(); iter != vn.endDescend(); ++iter)
                addRefPoint(*iter, vn);
        }

        /// Reset to the single point where the given Varnode is defined
        /// Any previous cover is removed. Calling this with an
        /// input Varnode still produces a valid Cover.
        /// \param vn is the Varnode
        public void addDefPoint(Varnode vn)
        {
            PcodeOp? def;

            cover.clear();

            def = vn.getDef();
            if (def != null) {
                CoverBlock & block(cover[def.getParent().getIndex()]);
                block.setBegin(def);    // Set the point topology
                block.setEnd(def);
            }
            else if (vn.isInput())
            {
                CoverBlock & block(cover[0]);
                block.setBegin((PcodeOp*)2 ); // Special mark for input
                block.setEnd((PcodeOp*)2 );
            }
        }

        /// Add a variable read to \b this Cover
        /// Given a Varnode being read and the PcodeOp which reads it,
        /// add the point of the read to \b this and recursively fill in backwards until
        /// we run into existing cover.
        /// \param ref is the reading PcodeOp
        /// \param vn is the Varnode being read
        public void addRefPoint(PcodeOp @ref, Varnode vn)
        {
            int j;
            FlowBlock* bl;
            uint ustop;

            bl = @ref.getParent();
            CoverBlock & block(cover[bl.getIndex()]);
            if (block.empty())
            {
                block.setEnd(@ref);
            }
            else
            {
                if (block.contain(@ref))
                {
                    if (@ref.code() != CPUI_MULTIEQUAL) return;
                    // Even if MULTIEQUAL ref is contained
                    // we may be adding new cover because we are
                    // looking at a different branch. So don't return
                }
                else
                {
                    PcodeOp* op = block.getStop();
                    PcodeOp* startop = block.getStart();
                    block.setEnd(@ref);      // Otherwise update endpoint
                    ustop = CoverBlock::getUIndex(block.getStop());
                    if (ustop >= CoverBlock::getUIndex(startop))
                    {
                        if ((op != (PcodeOp)null)&& (op != (PcodeOp*)2)&&
                            (op.code() == CPUI_MULTIEQUAL) && (startop == (PcodeOp)null)) {
                            // This block contains only an infinitesimal tip
                            // of cover through one branch of a MULTIEQUAL
                            // we still need to traverse through branches
                            for (j = 0; j < bl.sizeIn(); ++j)
                                addRefRecurse(bl.getIn(j));
                        }
                        return;
                    }
                }
            }
            //  if (bl.InSize()==0)
            //    throw new LowlevelError("Ref point is not in flow of defpoint");
            if (ref.code() == CPUI_MULTIEQUAL)
            {
                for (j = 0; j < ref.numInput(); ++j)
                    if (ref.getIn(j) == vn)
                        addRefRecurse(bl.getIn(j));
            }
            else
                for (j = 0; j < bl.sizeIn(); ++j)
                    addRefRecurse(bl.getIn(j));
        }

        //  void remove_refpoint(PcodeOp *ref,const Varnode *vn) {
        //    rebuild(vn); }		// Cheap but inefficient

        ///< Dump a description of \b this cover to stream
        /// \param s is the output stream
        public void print(ostream s)
        {
            Dictionary<int, CoverBlock>::const_iterator iter;

            for (iter = cover.begin(); iter != cover.end(); ++iter)
            {
                s << dec << (*iter).first << ": ";
                (*iter).second.print(s);
                s << endl;
            }
        }

        /// Get beginning of CoverBlocks
        public IEnumerator<KeyValuePair<int, CoverBlock>> begin()
        {
            return cover.GetEnumerator();
        }

        // Dictionary<int, CoverBlock>::const_iterator end(void) { return cover.end(); }		///< Get end of CoverBlocks
    }
}
