﻿using Sla.CORE;

namespace Sla.DECCORE
{
    /// \brief A kind of MemoryBank which retrieves its data from an underlying LoadImage
    ///
    /// Any bytes requested on the bank which lie in the LoadImage are retrieved from
    /// the LoadImage.  Other addresses in the space are filled in with zero.
    /// This bank cannot be written to.
    internal class MemoryImage : MemoryBank
    {
        /// The underlying LoadImage
        private LoadImage loader;

        /// Exception is thrown for write attempts
        protected override void insert(ulong addr, ulong val)
        {
            throw new LowlevelError("Writing to read-only MemoryBank");
        }

        /// Overridden find method
        /// Find an aligned word from the bank.  First an attempt is made to fetch the data from the
        /// LoadImage.  If this fails, the value is returned as 0.
        /// \param addr is the address of the word to fetch
        /// \return the fetched value
        internal override ulong find(ulong addr)
        {
            // Assume that -addr- is word aligned
            // Make sure all bytes start as 0, as load may not fill all bytes
            ulong res = 0;
            AddrSpace spc = getSpace();
            try {
                byte[] ptr = new byte[sizeof(ulong)];
                int index = (Globals.HOST_ENDIAN == 1)
                    ? (int)(sizeof(ulong) - getWordSize())
                    : 0;
                loader.loadFill(ptr, index, (int)getWordSize(), new Address(spc, addr));
            }
            catch (DataUnavailError) {
                // Pages not mapped in the load image, are assumed to be zero
                res = 0;
            }
            if ((Globals.HOST_ENDIAN == 1) != spc.isBigEndian())
                res = Globals.byte_swap(res, getWordSize());
            return res;
        }

        /// Overridded getPage method
        /// Retrieve an aligned page from the bank.  First an attempt is made to retrieve the
        /// page from the LoadImage, which may do its own zero filling.  If the attempt fails, the
        /// page is entirely filled in with zeros.
        internal override void getPage(ulong addr, byte[] res, uint skip, uint size)
        {
            // Assume that -addr- is page aligned
            AddrSpace spc = getSpace();

            try {
                loader.loadFill(res, 0, (int)size, new Address(spc, addr + skip));
            }
            catch (DataUnavailError) {
                // Pages not mapped in the load image, are assumed to be zero
                for (int i = 0; i < size; ++i)
                    res[i] = 0;
            }
        }

        /// Constructor for a loadimage memorybank
        /// A MemoryImage needs everything a basic memory bank needs and is needs to know
        /// the underlying LoadImage object to forward read reqests to.
        /// \param spc is the address space associated with the memory bank
        /// \param ws is the number of bytes in the preferred wordsize (must be power of 2)
        /// \param ps is the number of bytes in a page (must be power of 2)
        /// \param ld is the underlying LoadImage
        public MemoryImage(AddrSpace spc, int ws, int ps, LoadImage ld)
            : base(spc, (uint)ws, (uint)ps)
        {
            loader = ld;
        }
    }
}
