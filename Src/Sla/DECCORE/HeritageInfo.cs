using Sla.CORE;

namespace Sla.DECCORE
{
    /// \brief Information about heritage passes performed for a specific address space
    ///
    /// For a particular address space, this keeps track of:
    ///   - how long to delay heritage
    ///   - how long to delay dead code removal
    ///   - whether dead code has been removed (for this space)
    ///   - have warnings been issued
    internal class HeritageInfo
    {
        // friend class Heritage;
        /// The address space \b this record describes
        internal AddrSpace? space;
        /// How many passes to delay heritage of this space
        internal int delay;
        /// How many passes to delay deadcode removal of this space
        private int deadcodedelay;
        /// >0 if Varnodes in this space have been eliminated
        internal int deadremoved;
        /// \b true if the search for LOAD ops to guard has been performed
        internal bool loadGuardSearch;
        /// \b true if warning issued previously
        internal bool warningissued;
        /// \b true for the \e stack space, if stack placeholders have not been removed
        internal bool hasCallPlaceholders;

        /// Return \b true if heritage is performed on this space
        internal bool isHeritaged() => (space != (AddrSpace)null);

        /// Reset the state
        internal void reset()
        {
            // Leave any override intact: deadcodedelay = delay;
            deadremoved = 0;
            if (space != (AddrSpace)null)
                hasCallPlaceholders = (space.getType() == spacetype.IPTR_SPACEBASE);
            warningissued = false;
            loadGuardSearch = false;
        }

        /// Initialize heritage state information for a particular address space
        /// \param spc is the address space
        public HeritageInfo(AddrSpace spc)
        {
            if (spc == (AddrSpace)null) {
                space = (AddrSpace)null;
                delay = 0;
                deadcodedelay = 0;
                hasCallPlaceholders = false;
            }
            else if (!spc.isHeritaged()) {
                space = (AddrSpace)null;
                delay = spc.getDelay();
                deadcodedelay = spc.getDeadcodeDelay();
                hasCallPlaceholders = false;
            }
            else {
                space = spc;
                delay = spc.getDelay();
                deadcodedelay = spc.getDeadcodeDelay();
                hasCallPlaceholders = (spc.getType() == spacetype.IPTR_SPACEBASE);
            }
            deadremoved = 0;
            warningissued = false;
            loadGuardSearch = false;
        }
    }
}
