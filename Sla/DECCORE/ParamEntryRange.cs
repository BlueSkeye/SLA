using ghidra;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace ghidra
{
    /// \brief Class for storing ParamEntry objects in an interval range (rangemap)
    internal class ParamEntryRange
    {
        /// Starting offset of the ParamEntry's range
        private uintb first;
        /// Ending offset of the ParamEntry's range
        private uintb last;
        /// Position of the ParamEntry within the entire prototype list
        private int4 position;
        /// Pointer to the actual ParamEntry
        private ParamEntry entry;

        /// \brief Helper class for initializing ParamEntryRange in a range map
        internal class InitData
        {
            // friend class ParamEntryRange;
            /// Position (within the full list) being assigned to the ParamEntryRange
            private int4 position;
            /// Underlying ParamEntry being assigned to the ParamEntryRange
            private ParamEntry entry;
            
            public InitData(int pos, ParamEntry e)
            {
                position = pos;
                entry = e;
            }
        }

        /// \brief Helper class for subsorting on position
        internal class SubsortPosition
        {
            /// The position value
            private int position;

            /// Constructor for use with rangemap
            public SubsortPosition()
            {
            }

            /// Construct given position
            public SubsortPosition(int pos)
            {
                position = pos;
            }

            /// Constructor minimal/maximal subsort
            public SubsortPosition(bool val)
            {
                position = val ? 1000000 : 0;
            }
            public static bool operator <(SubsortPosition op1, SubsortPosition op2)
            {
                return op1.position<op2.position;
            }
        }

        //typedef uintb linetype;		///< The linear element for a rangemap
        //typedef SubsortPosition subsorttype;	///< The sub-sort object for a rangemap
        //typedef InitData inittype;		///< Initialization data for a ScopeMapper

        /// Initialize the range
        public ParamEntryRange(inittype data, uintb f, uintb l)
        {
            first = f; last = l; position = data.position; entry = data.entry;
        }

        /// Get the first address in the range
        public uintb getFirst() => first;

        /// Get the last address in the range
        public uintb getLast() => last;

        /// Get the sub-subsort object
        public subsorttype getSubsort() => new SubsortPosition(position);

        /// Get pointer to actual ParamEntry
        public ParamEntry getParamEntry() => entry;
    }
}
