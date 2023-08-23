
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
        //typedef Scope *inittype;		///< Initialization data for a ScopeMapper

        // friend class Database;
        /// \brief Helper class for \e not doing any sub-sorting of overlapping ScopeMapper ranges
        internal class NullSubsort
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

            ///< Compare operation (does nothing)
            public static bool operator <(NullSubsort op1, NullSubsort op2)
            {
                return false;
            }
        }

        /// The Scope owning this address range
        private Scope scope;
        /// The first address of the range
        private Address first;
        /// The last address of the range
        private Address last;

        /// Initialize the range (with the owning Scope)
        public ScopeMapper(inittype data, Address f, Address l)
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
    }
}
