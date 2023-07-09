using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mime;
using System.Text;
using System.Threading.Tasks;

namespace ghidra
{
    /// \brief A helper class for caching the active context blob to minimize database lookups
    /// This merely caches the last retrieved context blob ("array of words") and the range of
    /// addresses over which the blob is valid.  It encapsulates the ContextDatabase itself and
    /// exposes a minimal interface (getContext() and setContext()).
    public class ContextCache
    {
        /// The encapsulated context database
        private ContextDatabase database;
        /// If set to \b false, and setContext() call is dropped
        private bool allowset;
        /// Address space of the current valid range
        private AddrSpace? curspace;
        /// Starting offset of the current valid range
        private ulong first;
        /// Ending offset of the current valid range
        private ulong last;
        /// The current cached context blob
        private uint[]? context;

        /// Construct given a context database
        /// \param db is the context database that will be encapsulated
        public ContextCache(ContextDatabase db)
        {
            database = db;
            // Mark cache as invalid
            curspace = null;
            allowset = true;
        }

        /// Retrieve the encapsulated database object
        public ContextDatabase getDatabase()
        {
            return database;
        }

        /// Toggle whether setContext() calls are ignored
        public void allowSet(bool val)
        {
            allowset = val;
        }

        /// Retrieve the context blob for the given address
        /// Check if the address is in the current valid range. If it is, return the cached
        /// blob.  Otherwise, make a call to the database and cache a new block and valid range.
        /// \param addr is the given address
        /// \param buf is where the blob should be stored
        public void getContext(ref Address addr, uint[] buf)
        {
            if (   (addr.getSpace() != curspace) 
                || (first > addr.getOffset())
                || (last < addr.getOffset()))
            {
                curspace = addr.getSpace();
                context = database.getContext(addr, out first, out last);
            }
            if (null == context) {
                throw new BugException();
            }
            for (int i = 0; i < database.getContextSize(); ++i) {
                buf[i] = context[i];
            }
        }

        /// \brief Change the value of a context variable at the given address with no bound
        /// The context value is set starting at the given address and \e paints memory up
        /// to the next explicit change point.
        /// \param addr is the given starting address
        /// \param num is the word index of the context variable
        /// \param mask is the mask delimiting the context variable
        /// \param value is the (already shifted) value to set
        public void setContext(ref Address addr, int num, uint mask, uint value)
        {
            if (!allowset)
            {
                return;
            }
            database.setContextChangePoint(addr, num, mask, value);
            if ((addr.getSpace() == curspace) && (first <= addr.getOffset()) && (last >= addr.getOffset()))
            {
                // Invalidate cache
                curspace = null;
            }
        }

        /// \brief Change the value of a context variable across an explicit address range
        /// The context value is \e painted across the range. The context variable is marked as
        /// explicitly changing at the starting address of the range.
        /// \param addr1 is the starting address of the given range
        /// \param addr2 is the ending address of the given range
        /// \param num is the word index of the context variable
        /// \param mask is the mask delimiting the context variable
        /// \param value is the (already shifted) value to set
        public void setContext(ref Address addr1, ref Address addr2, int num, uint mask,
            uint value)
        {
            if (!allowset)
            {
                return;
            }
            database.setContextRegion(addr1, addr2, num, mask, value);
            if ((addr1.getSpace() == curspace)
                && (first <= addr1.getOffset())
                && (last >= addr1.getOffset()))
            {
                // Invalidate cache
                curspace = null;
            }
            if ((first <= addr2.getOffset()) && (last >= addr2.getOffset()))
            {
                // Invalidate cache
                curspace = null;
            }
            if ((first >= addr1.getOffset()) && (first <= addr2.getOffset()))
            {
                // Invalidate cache
                curspace = null;
            }
        }
    }
}
