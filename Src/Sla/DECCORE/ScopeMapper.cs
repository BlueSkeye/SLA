﻿using Sla.CORE;
using Sla.EXTRA;

namespace Sla.DECCORE
{
    /// \brief An Address range associated with the symbol Scope that owns it
    /// As part of a rangemap, this forms a map from addresses to
    /// \e namespace Scopes so that the decompiler can quickly find
    /// the \e namespace Scope that holds the Symbol it sees accessed.
    internal class ScopeMapper
    {
        //typedef Address linetype;		///< The linear element for a rangemap
        //typedef NullSubsort subsorttype;	///< The sub-sort object for a rangemap
        //typedef Scope inittype;		///< Initialization data for a ScopeMapper

        // The Scope owning this address range
        internal Scope scope;
        /// The first address of the range
        private Address first;
        /// The last address of the range
        private Address last;

        /// Initialize the range (with the owning Scope)
        public ScopeMapper(/*inittype*/ Scope data, Address f, Address l)
        {
            scope = data;
            first = f;
            last = l;
        }

        /// Get the first address in the range
        public Address getFirst() => first;

        /// Get the last address in the range
        public Address getLast() => last;

        /// Get the sub-subsort object
        public NullSubsort getSubsort() => new NullSubsort();

        /// Get the Scope owning this address range
        public Scope getScope() => scope;

        // friend class Database;
        // Helper class for \e not doing any sub-sorting of overlapping ScopeMapper ranges
        internal class NullSubsort : IComparable<NullSubsort>
        {
            public NullSubsort()
            {
            }

            /// Constructor given boolean
            public NullSubsort(bool val)
            {
            }

            ///< Copy constructor
            public NullSubsort(NullSubsort op2)
            {
            }

            public int CompareTo(NullSubsort? other)
            {
                if (other == null) throw new ApplicationException();
                if (this < other) return -1;
                if (this > other) return 1;
                return 0;
            }

            // Compare operation (does nothing)
            public static bool operator <(NullSubsort op1, NullSubsort op2)
            {
                return false;
            }

            public static bool operator >(NullSubsort op1, NullSubsort op2)
            {
                return false;
            }
        }

        internal class SubsorttypeInstanciator : IRangemapSubsortTypeInstantiator<NullSubsort>
        {
            internal static SubsorttypeInstanciator Instance = new SubsorttypeInstanciator();

            private SubsorttypeInstanciator()
            {
            }

            /// \brief Given a boolean value, construct the earliest/latest possible sub-sort
            /// \param val is \b true for the latest and \b false for the earliest possible sub-sort
            public NullSubsort Create(bool value)
            {
                return new NullSubsort(value);
            }

            public NullSubsort Create(NullSubsort cloned)
            {
                return new NullSubsort(cloned);
            }
        }
    }
}
