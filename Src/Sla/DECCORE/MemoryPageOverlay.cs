using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief Memory bank that overlays some other memory bank, using a "copy on write" behavior.
    ///
    /// Pages are copied from the underlying object only when there is
    /// a write. The underlying access routines are overridden to make optimal use
    /// of this page implementation.  The underlying memory bank can be a \b null pointer
    /// in which case, this memory bank behaves as if it were initially filled with zeros.
    internal class MemoryPageOverlay : MemoryBank
    {
        /// Underlying memory object
        private MemoryBank underlie;
        /// Overlayed pages
        private Dictionary<ulong, byte> page;

        /// Overridden aligned word insert
        /// This derived method looks for a previously cached page of the underlying memory bank.
        /// If the cached page does not exist, it creates it and fills in its initial value by
        /// retrieving the page from the underlying bank.  The new value is then written into
        /// cached page.
        /// \param addr is the aligned address of the word to be written
        /// \param val is the value to be written at that word
        protected override void insert(ulong addr, ulong val)
        {
            ulong pageaddr = addr & ~((ulong)(getPageSize() - 1));
            Dictionary<ulong, byte*>::iterator iter;

            byte* pageptr;

            iter = page.find(pageaddr);
            if (iter != page.end())
                pageptr = (*iter).second;
            else
            {
                pageptr = new byte[getPageSize()];
                page[pageaddr] = pageptr;
                if (underlie == (MemoryBank*)0)
                {
                    for (int i = 0; i < getPageSize(); ++i)
                        pageptr[i] = 0;
                }
                else
                    underlie.getPage(pageaddr, pageptr, 0, getPageSize());
            }

            ulong pageoffset = addr & ((ulong)(getPageSize() - 1));
            deconstructValue(pageptr + pageoffset, val, getWordSize(), getSpace().isBigEndian());
        }

        /// Overridden aligned word find
        /// This derived method first looks for the aligned word in the mapped pages. If the
        /// address is not mapped, the search is forwarded to the \e underlying memory bank.
        /// If there is no underlying bank, zero is returned.
        /// \param addr is the aligned offset of the word
        /// \return the retrieved value
        protected override ulong find(ulong addr)
        {
            ulong pageaddr = addr & ~((ulong)(getPageSize() - 1));
            Dictionary<ulong, byte*>::const_iterator iter;

            iter = page.find(pageaddr);
            if (iter == page.end())
            {
                if (underlie == (MemoryBank*)0)
                    return (ulong)0;
                return underlie.find(addr);
            }

            byte* pageptr = (*iter).second;

            ulong pageoffset = addr & ((ulong)(getPageSize() - 1));
            return constructValue(pageptr + pageoffset, getWordSize(), getSpace().isBigEndian());
        }

        /// Overridden getPage
        /// The desired page is looked for in the page cache.  If it doesn't exist, the
        /// request is forwarded to \e underlying bank.  If there is no underlying bank, the
        /// result buffer is filled with zeros.
        /// \param addr is the aligned offset of the page
        /// \param res is the pointer to where retrieved bytes should be stored
        /// \param skip is the offset \e into \e the \e page from where bytes should be retrieved
        /// \param size is the number of bytes to retrieve
        protected override void getPage(ulong addr, byte[] res, int skip, int size)
        {
            Dictionary<ulong, byte*>::const_iterator iter;

            iter = page.find(addr);
            if (iter == page.end())
            {
                if (underlie == (MemoryBank*)0)
                {
                    for (int i = 0; i < size; ++i)
                        res[i] = 0;
                    return;
                }
                underlie.getPage(addr, res, skip, size);
                return;
            }
            byte* pageptr = (*iter).second;
            memcpy(res, pageptr + skip, size);
        }

        /// Overridden setPage
        /// First, a cached version of the desired page is searched for via its address. If it doesn't
        /// exist, it is created, and its initial value is filled via the \e underlying bank. The bytes
        /// to be written are then copied into the cached page.
        /// \param addr is the aligned offset of the page to write
        /// \param val is a pointer to bytes to be written into the page
        /// \param skip is the offset \e into \e the \e page where bytes should be written
        /// \param size is the number of bytes to write
        protected override void setPage(ulong addr, byte[] val, int skip,int size)
        {
            Dictionary<ulong, byte*>::iterator iter;
            byte* pageptr;

            iter = page.find(addr);
            if (iter == page.end())
            {
                pageptr = new byte[getPageSize()];
                page[addr] = pageptr;
                if (size != getPageSize())
                {
                    if (underlie == (MemoryBank*)0)
                    {
                        for (int i = 0; i < getPageSize(); ++i)
                            pageptr[i] = 0;
                    }
                    else
                        underlie.getPage(addr, pageptr, 0, getPageSize());
                }
            }
            else
                pageptr = (*iter).second;

            memcpy(pageptr + skip, val, size);
        }

        ///< Constructor for page overlay
        /// A page overlay memory bank needs all the parameters for a generic memory bank
        /// and it needs to know the underlying memory bank being overlayed.
        /// \param spc is the address space associated with the memory bank
        /// \param ws is the number of bytes in the preferred wordsize (must be power of 2)
        /// \param ps is the number of bytes in a page (must be power of 2)
        /// \param ul is the underlying MemoryBank
        public MemoryPageOverlay(AddrSpace spc, int ws, int ps, MemoryBank ul)
            : base(spc, ws, ps)
        {
            underlie = ul;
        }

        ~MemoryPageOverlay()
        {
            Dictionary<ulong, byte*>::iterator iter;

            for (iter = page.begin(); iter != page.end(); ++iter)
                delete[](*iter).second;
        }
    }
}
