﻿using ghidra;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Sla.DECCORE
{
    /// \brief Class for storing ParamEntry objects in an interval range (rangemap)
    internal class ParamEntryRange
    {
        /// Starting offset of the ParamEntry's range
        private ulong first;
        /// Ending offset of the ParamEntry's range
        private ulong last;
        /// Position of the ParamEntry within the entire prototype list
        private int position;
        /// Pointer to the actual ParamEntry
        private ParamEntry entry;

        /// \brief Helper class for initializing ParamEntryRange in a range map
        internal class InitData
        {
            // friend class ParamEntryRange;
            /// Position (within the full list) being assigned to the ParamEntryRange
            private int position;
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

        //typedef ulong linetype;		///< The linear element for a rangemap
        //typedef SubsortPosition subsorttype;	///< The sub-sort object for a rangemap
        //typedef InitData inittype;		///< Initialization data for a ScopeMapper

        /// Initialize the range
        public ParamEntryRange(inittype data, ulong f, ulong l)
        {
            first = f; last = l; position = data.position; entry = data.entry;
        }

        /// Get the first address in the range
        public ulong getFirst() => first;

        /// Get the last address in the range
        public ulong getLast() => last;

        /// Get the sub-subsort object
        public subsorttype getSubsort() => new SubsortPosition(position);

        /// Get pointer to actual ParamEntry
        public ParamEntry getParamEntry() => entry;
    }
}
