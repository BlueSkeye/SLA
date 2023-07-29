using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
        private AddrSpace space;
        /// How many passes to delay heritage of this space
        private int4 delay;
        /// How many passes to delay deadcode removal of this space
        private int4 deadcodedelay;
        /// >0 if Varnodes in this space have been eliminated
        private int4 deadremoved;
        /// \b true if the search for LOAD ops to guard has been performed
        private bool loadGuardSearch;
        /// \b true if warning issued previously
        private bool warningissued;
        /// \b true for the \e stack space, if stack placeholders have not been removed
        private bool hasCallPlaceholders;

        /// Return \b true if heritage is performed on this space
        private bool isHeritaged() => (space != (AddrSpace*)0);

        /// Reset the state
        private void reset()
        {
            // Leave any override intact: deadcodedelay = delay;
            deadremoved = 0;
            if (space != (AddrSpace*)0)
                hasCallPlaceholders = (space->getType() == IPTR_SPACEBASE);
            warningissued = false;
            loadGuardSearch = false;
        }

        /// Initialize heritage state information for a particular address space
        /// \param spc is the address space
        public HeritageInfo(AddrSpace spc)
        {
            if (spc == (AddrSpace*)0)
            {
                space = (AddrSpace*)0;
                delay = 0;
                deadcodedelay = 0;
                hasCallPlaceholders = false;
            }
            else if (!spc->isHeritaged())
            {
                space = (AddrSpace*)0;
                delay = spc->getDelay();
                deadcodedelay = spc->getDeadcodeDelay();
                hasCallPlaceholders = false;
            }
            else
            {
                space = spc;
                delay = spc->getDelay();
                deadcodedelay = spc->getDeadcodeDelay();
                hasCallPlaceholders = (spc->getType() == IPTR_SPACEBASE);
            }
            deadremoved = 0;
            warningissued = false;
            loadGuardSearch = false;
        }
    }
}
