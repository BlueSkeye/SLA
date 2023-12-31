﻿using Sla.CORE;

namespace Sla.DECCORE
{
    /// \brief A record describing a section bytes in the executable
    ///
    /// A lightweight object specifying the location and size of the section and basic properties
    internal struct LoadImageSection
    {
        /// Boolean properties a section might have
        [Flags()]
        internal enum Properties
        {
            /// Not allocated in memory (debug info)
            unalloc = 1,
            /// uninitialized section
            noload = 2,
            /// code only
            code = 4,
            /// data only
            data = 8,
            /// read only section
            @readonly = 16
        }
        /// Starting address of section
        internal Address address;
        /// Number of bytes in section
        internal ulong size;
        /// Properties of the section
        internal Properties flags;
    }
}
