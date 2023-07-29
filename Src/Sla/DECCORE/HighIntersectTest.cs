using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief A cache of Cover intersection tests for HighVariables
    ///
    /// An test is performed by calling the intersect() method, which returns the result of a full
    /// Cover intersection test, taking into account, overlapping pieces, shadow Varnodes etc. The
    /// results of the test are cached in \b this object, so repeated calls do not need to perform the
    /// full calculation.  The cache examines HighVariable dirtiness flags to determine if its Cover
    /// and cached tests are stale.  The Cover can be externally updated, without performing a test,
    /// and still keeping the cached tests accurate, by calling the updateHigh() method.  If two HighVariables
    /// to be merged, the cached tests can be updated by calling moveIntersectTest() before merging.
    internal class HighIntersectTest
    {
        /// A cache of intersection tests, sorted by HighVariable pair
        private Dictionary<HighEdge, bool> highedgemap;

        /// \brief Gather Varnode instances of the given HighVariable that intersect a cover on a specific block
        ///
        /// \param a is the given HighVariable
        /// \param blk is the specific block number
        /// \param cover is the Cover to test for intersection
        /// \param res will hold the resulting intersecting Varnodes
        private static void gatherBlockVarnodes(HighVariable a, int blk, Cover cover, List<Varnode> res)
        {
            for (int i = 0; i < a.numInstances(); ++i)
            {
                Varnode* vn = a.getInstance(i);
                if (1 < vn.getCover().intersectByBlock(blk, cover))
                    res.push_back(vn);
            }
        }

        /// \brief Test instances of a the given HighVariable for intersection on a specific block with a cover
        ///
        /// A list of Varnodes has already been determined to intersect on the block.  For an instance that does as
        /// well, a final test of copy shadowing is performed with the Varnode list.  If there is no shadowing,
        /// a merging intersection has been found and \b true is returned.
        /// \param a is the given HighVariable
        /// \param blk is the specific block number
        /// \param cover is the Cover to test for intersection
        /// \param relOff is the relative byte offset of the HighVariable to the Varnodes
        /// \param blist is the list of Varnodes for copy shadow testing
        /// \return \b true if there is an intersection preventing merging
        private static bool testBlockIntersection(HighVariable a, int blk, Cover cover,int relOff,
            List<Varnode> blist)
        {
            for (int i = 0; i < a.numInstances(); ++i)
            {
                Varnode* vn = a.getInstance(i);
                if (2 > vn.getCover().intersectByBlock(blk, cover)) continue;
                for (int j = 0; j < blist.size(); ++j)
                {
                    Varnode* vn2 = blist[j];
                    if (1 < vn2.getCover().intersectByBlock(blk, *vn.getCover()))
                    {
                        if (vn.getSize() == vn2.getSize())
                        {
                            if (!vn.copyShadow(vn2))
                                return true;
                        }
                        else
                        {
                            if (!vn.partialCopyShadow(vn2, relOff))
                                return true;
                        }
                    }
                }
            }
            return false;
        }

        /// \brief Test if two HighVariables intersect on a given BlockBasic
        ///
        /// Intersections are checked only on the specified block.
        /// \param a is the first HighVariable
        /// \param b is the second HighVariable
        /// \param blk is the index of the BlockBasic on which to test intersection
        /// \return \b true if an intersection occurs in the specified block
        private bool blockIntersection(HighVariable a, HighVariable b, int blk)
        {
            List<Varnode*> blist;

            Cover aCover = a.getCover();
            Cover bCover = b.getCover();
            gatherBlockVarnodes(b, blk, aCover, blist);
            if (testBlockIntersection(a, blk, bCover, 0, blist))
                return true;
            if (a.piece != (VariablePiece*)0)
            {
                int baseOff = a.piece.getOffset();
                for (int i = 0; i < a.piece.numIntersection(); ++i)
                {
                    VariablePiece interPiece = a.piece.getIntersection(i);
                    int off = interPiece.getOffset() - baseOff;
                    if (testBlockIntersection(interPiece.getHigh(), blk, bCover, off, blist))
                        return true;
                }
            }
            if (b.piece != (VariablePiece*)0)
            {
                int bBaseOff = b.piece.getOffset();
                for (int i = 0; i < b.piece.numIntersection(); ++i)
                {
                    blist.clear();
                    VariablePiece bPiece = b.piece.getIntersection(i);
                    int bOff = bPiece.getOffset() - bBaseOff;
                    gatherBlockVarnodes(bPiece.getHigh(), blk, aCover, blist);
                    if (testBlockIntersection(a, blk, bCover, -bOff, blist))
                        return true;
                    if (a.piece != (VariablePiece*)0)
                    {
                        int baseOff = a.piece.getOffset();
                        for (int j = 0; j < a.piece.numIntersection(); ++j)
                        {
                            VariablePiece interPiece = a.piece.getIntersection(j);
                            int off = (interPiece.getOffset() - baseOff) - bOff;
                            if (off > 0 && off >= bPiece.getSize()) continue;      // Do a piece and b piece intersect at all
                            if (off < 0 && -off >= interPiece.getSize()) continue;
                            if (testBlockIntersection(interPiece.getHigh(), blk, bCover, off, blist))
                                return true;
                        }
                    }
                }
            }
            return false;
        }

        /// Remove cached intersection tests for a given HighVariable
        /// All tests for pairs where either the first or second HighVariable matches the given one
        /// are removed.
        /// \param high is the given HighVariable to purge
        private void purgeHigh(HighVariable high)
        {
            map<HighEdge, bool>::iterator iterfirst = highedgemap.lower_bound(HighEdge(high, (HighVariable*)0));
            map<HighEdge, bool>::iterator iterlast = highedgemap.lower_bound(HighEdge(high, (HighVariable*)~((ulong)0)));

            if (iterfirst == iterlast) return;
            --iterlast;         // Move back 1 to prevent deleting under the iterator
            map<HighEdge, bool>::iterator iter;
            for (iter = iterfirst; iter != iterlast; ++iter)
                highedgemap.erase(HighEdge((*iter).first.b, (*iter).first.a));
            highedgemap.erase(HighEdge((*iter).first.b, (*iter).first.a));
            ++iterlast;         // Restore original range (with possibly new open endpoint)

            highedgemap.erase(iterfirst, iterlast);
        }

        /// \brief Translate any intersection tests for \e high2 into tests for \e high1
        ///
        /// The two variables will be merged and \e high2, as an object, will be freed.
        /// We update the cached intersection tests for \e high2 so that they will now apply to new merged \e high1
        /// \param high1 is the variable object being kept
        /// \param high2 is the variable object being eliminated
        public void moveIntersectTests(HighVariable high1, HighVariable high2)
        {
            List<HighVariable*> yesinter;     // Highs that high2 intersects
            List<HighVariable*> nointer;      // Highs that high2 does not intersect
            map<HighEdge, bool>::iterator iterfirst = highedgemap.lower_bound(HighEdge(high2, (HighVariable*)0));
            map<HighEdge, bool>::iterator iterlast = highedgemap.lower_bound(HighEdge(high2, (HighVariable*)~((ulong)0)));
            map<HighEdge, bool>::iterator iter;

            for (iter = iterfirst; iter != iterlast; ++iter)
            {
                HighVariable* b = (*iter).first.b;
                if (b == high1) continue;
                if ((*iter).second)     // Save all high2's intersections
                    yesinter.push_back(b);  // as they are still valid for the merge
                else
                {
                    nointer.push_back(b);
                    b.setMark();       // Mark that high2 did not intersect
                }
            }
            // Do a purge of all high2's tests
            if (iterfirst != iterlast)
            {   // Delete all the high2 tests
                --iterlast;         // Move back 1 to prevent deleting under the iterator
                for (iter = iterfirst; iter != iterlast; ++iter)
                    highedgemap.erase(HighEdge((*iter).first.b, (*iter).first.a));
                highedgemap.erase(HighEdge((*iter).first.b, (*iter).first.a));
                ++iterlast;         // Restore original range (with possibly new open endpoint)

                highedgemap.erase(iterfirst, iterlast);
            }

            iter = highedgemap.lower_bound(HighEdge(high1, (HighVariable*)0));
            while ((iter != highedgemap.end()) && ((*iter).first.a == high1))
            {
                if (!(*iter).second)
                {   // If test is intersection==false
                    if (!(*iter).first.b.isMark()) // and there was no test with high2
                        highedgemap.erase(iter++); // Delete the test
                    else
                        ++iter;
                }
                else            // Keep any intersection==true tests
                    ++iter;
            }
            List<HighVariable*>::iterator titer;
            for (titer = nointer.begin(); titer != nointer.end(); ++titer)
                (*titer).clearMark();

            // Reinsert high2's intersection==true tests for high1 now
            for (titer = yesinter.begin(); titer != yesinter.end(); ++titer)
            {
                highedgemap[HighEdge(high1, *titer)] = true;
                highedgemap[HighEdge(*titer, high1)] = true;
            }
        }

        /// Make sure given HighVariable's Cover is up-to-date
        /// As manipulations are made, Cover information gets out of date. A \e dirty flag is used to
        /// indicate a particular HighVariable Cover is out-of-date.  This routine checks the \e dirty
        /// flag and updates the Cover information if it is set.
        /// \param a is the HighVariable to update
        /// \return \b true if the HighVariable was not originally dirty
        public bool updateHigh(HighVariable a)
        {
            if (!a.isCoverDirty()) return true;

            a.updateCover();
            purgeHigh(a);
            return false;
        }

        /// \brief Test the intersection of two HighVariables and cache the result
        ///
        /// If the Covers of the two variables intersect, this routine returns \b true. To avoid
        /// expensive computation on the Cover objects themselves, the test result associated with
        /// the pair of HighVariables is cached.
        /// \param a is the first HighVariable
        /// \param b is the second HighVariable
        /// \return \b true if the variables intersect
        public bool intersection(HighVariable a, HighVariable b)
        {
            if (a == b) return false;
            bool ares = updateHigh(a);
            bool bres = updateHigh(b);
            if (ares && bres)
            {       // If neither high was dirty
                map<HighEdge, bool>::iterator iter = highedgemap.find(HighEdge(a, b));
                if (iter != highedgemap.end()) // If previous test is present
                    return (*iter).second;  // Use it
            }

            bool res = false;
            int blk;
            List<int> blockisect;
            a.getCover().intersectList(blockisect, b.getCover(), 2);
            for (blk = 0; blk < blockisect.size(); ++blk)
            {
                if (blockIntersection(a, b, blockisect[blk]))
                {
                    res = true;
                    break;
                }
            }
            highedgemap[HighEdge(a, b)] = res; // Cache the result
            highedgemap[HighEdge(b, a)] = res;
            return res;
        }

        public void clear()
        {
            highedgemap.clear();
        }
    }
}
