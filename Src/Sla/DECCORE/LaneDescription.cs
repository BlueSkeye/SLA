using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief Description of logical lanes within a \b big Varnode
    ///
    /// A \b lane is a byte offset and size within a Varnode. Lanes within a
    /// Varnode are disjoint. In general, we expect a Varnode to be tiled with
    /// lanes all of the same size, but the API allows for possibly non-uniform lanes.
    internal class LaneDescription
    {
        /// Size of the region being split in bytes
        private int4 wholeSize;
        /// Size of lanes in bytes
        private List<int4> laneSize;
        /// Significance positions of lanes in bytes
        private List<int4> lanePosition;

        /// Copy constructor
        /// \param op2 is the lane description to copy from
        public LaneDescription(LaneDescription op2)
        {
            wholeSize = op2.wholeSize;
            laneSize = op2.laneSize;
            lanePosition = op2.lanePosition;
        }

        /// Construct uniform lanes
        /// Create lanes that are all the same size
        /// \param origSize is the size of the whole in bytes
        /// \param sz is the size of a lane in bytes
        public LaneDescription(int4 origSize, int4 sz)
        {
            wholeSize = origSize;
            int4 numLanes = origSize / sz;
            laneSize.resize(numLanes);
            lanePosition.resize(numLanes);
            int4 pos = 0;
            for (int4 i = 0; i < numLanes; ++i)
            {
                laneSize[i] = sz;
                lanePosition[i] = pos;
                pos += sz;
            }
        }

        /// Construct two lanes of arbitrary size
        /// \param origSize is the size of the whole in bytes
        /// \param lo is the size of the least significant lane in bytes
        /// \param hi is the size of the most significant lane in bytes
        public LaneDescription(int4 origSize, int4 lo, int4 hi)
        {
            wholeSize = origSize;
            laneSize.resize(2);
            lanePosition.resize(2);
            laneSize[0] = lo;
            laneSize[1] = hi;
            lanePosition[0] = 0;
            lanePosition[1] = lo;
        }

        /// Trim \b this to a subset of the original lanes
        /// Given a subrange, specified as an offset into the whole and size,
        /// throw out any lanes in \b this that aren't in the subrange, so that the
        /// size of whole is the size of the subrange.  If the subrange intersects partially
        /// with any of the lanes, return \b false.
        /// \param lsbOffset is the number of bytes to remove from the front of the description
        /// \param size is the number of bytes in the subrange
        /// \return \b true if \b this was successfully transformed to the subrange
        public bool subset(int4 lsbOffset, int4 size)
        {
            if (lsbOffset == 0 && size == wholeSize)
                return true;            // subrange is the whole range
            int4 firstLane = getBoundary(lsbOffset);
            if (firstLane < 0) return false;
            int4 lastLane = getBoundary(lsbOffset + size);
            if (lastLane < 0) return false;
            List<int4> newLaneSize;
            lanePosition.clear();
            int4 newPosition = 0;
            for (int4 i = firstLane; i < lastLane; ++i)
            {
                int4 sz = laneSize[i];
                lanePosition.push_back(newPosition);
                newLaneSize.push_back(sz);
                newPosition += sz;
            }
            wholeSize = size;
            laneSize = newLaneSize;
            return true;
        }

        /// Get the total number of lanes
        public int4 getNumLanes() => laneSize.size();

        /// Get the size of the region being split
        public int4 getWholeSize() => wholeSize;

        /// Get the size of the i-th lane
        public int4 getSize(int4 i) => laneSize[i];

        /// Get the significance offset of the i-th lane
        public int4 getPosition(int4 i) => lanePosition[i];

        /// Get index of lane that starts at the given byte position
        /// Position 0 will map to index 0 and a position equal to whole size will
        /// map to the number of lanes.  Positions that are out of bounds or that do
        /// not fall on a lane boundary will return -1.
        /// \param bytePos is the given byte position to test
        /// \return the index of the lane that start at the given position
        public int4 getBoundary(int4 bytePos)
        {
            if (bytePos < 0 || bytePos > wholeSize)
                return -1;
            if (bytePos == wholeSize)
                return lanePosition.size();
            int4 min = 0;
            int4 max = lanePosition.size() - 1;
            while (min <= max)
            {
                int4 index = (min + max) / 2;
                int4 pos = lanePosition[index];
                if (pos == bytePos) return index;
                if (pos < bytePos)
                    min = index + 1;
                else
                    max = index - 1;
            }
            return -1;
        }

        /// \brief Decide if a given truncation is natural for \b this description
        ///
        /// A subset of lanes are specified and a truncation (given by a byte position and byte size).
        /// If the truncation, relative to the subset, contains at least 1 lane and does not split any
        /// lanes, then return \b true and pass back the number of lanes and starting lane of the truncation.
        /// \param numLanes is the number of lanes in the original subset
        /// \param skipLanes is the starting (least significant) lane index of the original subset
        /// \param bytePos is the number of bytes to truncate from the front (least significant portion) of the subset
        /// \param size is the number of bytes to include in the truncation
        /// \param resNumLanes will hold the number of lanes in the truncation
        /// \param resSkipLanes will hold the starting lane in the truncation
        /// \return \b true if the truncation is natural
        public bool restriction(int4 numLanes, int4 skipLanes, int4 bytePos, int4 size, int4 resNumLanes,
            int4 resSkipLanes)
        {
            resSkipLanes = getBoundary(lanePosition[skipLanes] + bytePos);
            if (resSkipLanes < 0) return false;
            int4 finalIndex = getBoundary(lanePosition[skipLanes] + bytePos + size);
            if (finalIndex < 0) return false;
            resNumLanes = finalIndex - resSkipLanes;
            return (resNumLanes != 0);
        }

        /// \brief Decide if a given subset of lanes can be extended naturally for \b this description
        ///
        /// A subset of lanes are specified and their position within an extension (given by a byte position).
        /// The size in bytes of the extension is also given. If the extension is contained within \b this description,
        /// and the boundaries of the extension don't split any lanes, then return \b true and pass back
        /// the number of lanes and starting lane of the extension.
        /// \param numLanes is the number of lanes in the original subset
        /// \param skipLanes is the starting (least significant) lane index of the original subset
        /// \param bytePos is the number of bytes to truncate from the front (least significant portion) of the extension
        /// \param size is the number of bytes in the extension
        /// \param resNumLanes will hold the number of lanes in the extension
        /// \param resSkipLanes will hold the starting lane in the extension
        /// \return \b true if the extension is natural
        public bool extension(int4 numLanes, int4 skipLanes, int4 bytePos, int4 size, int4 resNumLanes,
            int4 resSkipLanes)
        {
            resSkipLanes = getBoundary(lanePosition[skipLanes] - bytePos);
            if (resSkipLanes < 0) return false;
            int4 finalIndex = getBoundary(lanePosition[skipLanes] - bytePos + size);
            if (finalIndex < 0) return false;
            resNumLanes = finalIndex - resSkipLanes;
            return (resNumLanes != 0);
        }
    }
}
