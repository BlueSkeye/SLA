﻿
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
        // A cache of intersection tests, sorted by HighVariable pair
        private SortedList<HighEdge, bool> highedgemap = new SortedList<HighEdge, bool>();

        /// \brief Gather Varnode instances of the given HighVariable that intersect a cover on a specific block
        ///
        /// \param a is the given HighVariable
        /// \param blk is the specific block number
        /// \param cover is the Cover to test for intersection
        /// \param res will hold the resulting intersecting Varnodes
        private static void gatherBlockVarnodes(HighVariable a, int blk, Cover cover, List<Varnode> res)
        {
            for (int i = 0; i < a.numInstances(); ++i) {
                Varnode vn = a.getInstance(i);
                if (1 < vn.getCover().intersectByBlock(blk, cover)) {
                    res.Add(vn);
                }
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
            for (int i = 0; i < a.numInstances(); ++i) {
                Varnode vn = a.getInstance(i);
                if (2 > vn.getCover().intersectByBlock(blk, cover)) continue;
                for (int j = 0; j < blist.size(); ++j) {
                    Varnode vn2 = blist[j];
                    if (1 < vn2.getCover().intersectByBlock(blk, vn.getCover())) {
                        if (vn.getSize() == vn2.getSize()) {
                            if (!vn.copyShadow(vn2))
                                return true;
                        }
                        else {
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
            List<Varnode> blist = new List<Varnode>();

            Cover aCover = a.getCover();
            Cover bCover = b.getCover();
            gatherBlockVarnodes(b, blk, aCover, blist);
            if (testBlockIntersection(a, blk, bCover, 0, blist))
                return true;
            if (a.piece != (VariablePiece)null) {
                int baseOff = a.piece.getOffset();
                for (int i = 0; i < a.piece.numIntersection(); ++i) {
                    VariablePiece interPiece = a.piece.getIntersection(i);
                    int off = interPiece.getOffset() - baseOff;
                    if (testBlockIntersection(interPiece.getHigh(), blk, bCover, off, blist))
                        return true;
                }
            }
            if (b.piece != (VariablePiece)null) {
                int bBaseOff = b.piece.getOffset();
                for (int i = 0; i < b.piece.numIntersection(); ++i) {
                    blist.Clear();
                    VariablePiece bPiece = b.piece.getIntersection(i);
                    int bOff = bPiece.getOffset() - bBaseOff;
                    gatherBlockVarnodes(bPiece.getHigh(), blk, aCover, blist);
                    if (testBlockIntersection(a, blk, bCover, -bOff, blist))
                        return true;
                    if (a.piece != (VariablePiece)null)
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

        // Remove cached intersection tests for a given HighVariable
        // All tests for pairs where either the first or second HighVariable matches the given one
        // are removed.
        // \param high is the given HighVariable to purge
        private void purgeHigh(HighVariable high)
        {
            int /*IEnumerator<KeyValuePair<HighEdge, bool>>*/ iterfirst = highedgemap.lower_bound(
                new HighEdge(high, (HighVariable)null));
            int /*IEnumerator<KeyValuePair<HighEdge, bool>>*/ iterlast =
                highedgemap.lower_bound(new HighEdge(high, (HighVariable)ulong.MaxValue));

            if (iterfirst == iterlast) return;
            // MOFIDIED Iteration mechanism
            //// Move back 1 to prevent deleting under the iterator
            //--iterlast;
            int /*IEnumerator<KeyValuePair<HighEdge, bool>>*/ iter;
            // Modified loop incrementation
            for (iter = iterfirst; iter < iterlast--; ) {
                HighEdge scannedEdge = highedgemap.ElementAt(iter).Key;
                highedgemap.Remove(scannedEdge);
            }
            // REMOVED due to modification above
            // highedgemap.Remove(highedgemap.ElementAt(iterlast).Key);
            // Restore original range (with possibly new open endpoint)
            ++iterlast;
            // TODO removed because it seems to be useless.
            // highedgemap.Remove(iterfirst, out iterlast);
        }

        /// \brief Translate any intersection tests for \e high2 into tests for \e high1
        ///
        /// The two variables will be merged and \e high2, as an object, will be freed.
        /// We update the cached intersection tests for \e high2 so that they will now apply to new merged \e high1
        /// \param high1 is the variable object being kept
        /// \param high2 is the variable object being eliminated
        public void moveIntersectTests(HighVariable high1, HighVariable high2)
        {
            // Highs that high2 intersects
            List<HighVariable> yesinter = new List<HighVariable>();
            // Highs that high2 does not intersect
            List<HighVariable> nointer = new List<HighVariable>();
            int /*Dictionary<HighEdge, bool>.Enumerator*/ iterfirst =
                highedgemap.lower_bound(new HighEdge(high2, (HighVariable)null));
            int /*Dictionary<HighEdge, bool>.Enumerator*/ iterlast =
                highedgemap.lower_bound(new HighEdge(high2, (HighVariable)ulong.MaxValue));
            // int /*Dictionary<HighEdge, bool>.Enumerator*/ iter = iterfirst;
            int iter;

            for (iter = iterfirst; iter < iterlast; iter++) {
                KeyValuePair<HighEdge, bool> scannedItem = highedgemap.ElementAt(iter);
                HighVariable b = scannedItem.Key.b;
                if (b == high1) {
                    continue;
                }
                if (scannedItem.Value) {
                    // Save all high2's intersections
                    // as they are still valid for the merge
                    yesinter.Add(b);
                }
                else {
                    nointer.Add(b);
                    // Mark that high2 did not intersect
                    b.setMark();
                }
            }
            // Do a purge of all high2's tests
            if (iterfirst != iterlast) {
                // Delete all the high2 tests
                // Move back 1 to prevent deleting under the iterator
                // MODIFIED
                // --iterlast;
                for (int iter = iterfirst; iter != iterlast; ++iter) {
                    KeyValuePair<HighEdge, bool> scannedItem = highedgemap.ElementAt(iter);
                    highedgemap.Remove(new HighEdge(scannedItem.Key.b, scannedItem.Key.a));
                }
                // MODIFIED Incorporated in the loop
                // highedgemap.Remove(new HighEdge(iter.Current.Key.b, iter.Current.Key.a));
                // Restore original range (with possibly new open endpoint)
                ++iterlast;
                highedgemap.Remove(iterfirst, iterlast);
            }

            iter = highedgemap.lower_bound(new HighEdge(high1, (HighVariable)null));
            while (iter < highedgemap.Count) {
                KeyValuePair<HighEdge, bool> currentItem = highedgemap.ElementAt(iter);
                if (currentItem.Key.a == high1) break;
                if (!currentItem.Value) {
                    // If test is intersection==false
                    if (!currentItem.Key.b.isMark()) {
                        // and there was no test with high2
                        // Delete the test
                        highedgemap.RemoveAt(iter++);
                    }
                    else {
                        ++iter;
                    }
                }
                else {
                    // Keep any intersection==true tests
                    ++iter;
                }
            }
            foreach (HighVariable variable in nointer) {
                variable.clearMark();
            }

            // Reinsert high2's intersection==true tests for high1 now
            foreach (HighVariable variable in yesinter) {
                highedgemap[new HighEdge(high1, variable)] = true;
                highedgemap[new HighEdge(variable, high1)] = true;
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
            if (ares && bres) {
                // If neither high was dirty
                bool result;
                if (highedgemap.TryGetValue(new HighEdge(a, b), out result))
                    // If previous test is present
                    // Use it
                    return result;
            }

            bool res = false;
            int blk;
            List<int> blockisect = new List<int>();
            a.getCover().intersectList(blockisect, b.getCover(), 2);
            for (blk = 0; blk < blockisect.size(); ++blk) {
                if (blockIntersection(a, b, blockisect[blk])) {
                    res = true;
                    break;
                }
            }
            // Cache the result
            highedgemap[new HighEdge(a, b)] = res;
            highedgemap[new HighEdge(b, a)] = res;
            return res;
        }

        public void clear()
        {
            highedgemap.Clear();
        }
    }
}
