using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief All storage/state for a pcode machine
    ///
    /// Every piece of information in a pcode machine is representable as a triple
    /// (AddrSpace,offset,size).  This class allows getting and setting
    /// of all state information of this form.
    internal class MemoryState
    {
        /// Architecture information about memory spaces
        protected Translate trans;
        /// Memory banks associated with each address space
        protected List<MemoryBank> memspace;

        /// A constructor for MemoryState
        /// The MemoryState needs a Translate object in order to be able to convert register names
        /// into varnodes
        /// \param t is the translator
        public MemoryState(Translate t)
        {
            trans = t;
        }

        ~MemoryState()
        {
        }

        /// Get the Translate object
        /// Retrieve the actual pcode translator being used by this machine state
        /// \return a pointer to the Translate object
        public Translate getTranslate() => trans;

        /// Map a memory bank into the state
        /// MemoryBanks associated with specific address spaces must be registers with this MemoryState
        /// via this method.  Each address space that will be used during emulation must be registered
        /// separately.  The MemoryState object does \e not assume responsibility for freeing the MemoryBank
        /// \param bank is a pointer to the MemoryBank to be registered
        public void setMemoryBank(MemoryBank bank)
        {
            AddrSpace spc = bank.getSpace();
            int index = spc.getIndex();

            while (index >= memspace.size())
                memspace.Add((MemoryBank*)0);

            memspace[index] = bank;
        }

        /// Get a memory bank associated with a particular space
        /// Any MemoryBank that has been registered with this MemoryState can be retrieved via this
        /// method if the MemoryBank's associated address space is known.
        /// \param spc is the address space of the desired MemoryBank
        /// \return a pointer to the MemoryBank or \b null if no bank is associated with \e spc.
        public MemoryBank getMemoryBank(AddrSpace spc)
        {
            int index = spc.getIndex();
            if (index >= memspace.size())
                return (MemoryBank*)0;
            return memspace[index];
        }

        /// Set a value on the memory state
        /// This is the main interface for writing values to the MemoryState.
        /// If there is no registered MemoryBank for the desired address space, or
        /// if there is some other error, an exception is thrown.
        /// \param spc is the address space to write to
        /// \param off is the offset where the value should be written
        /// \param size is the number of bytes to be written
        /// \param cval is the value to be written
        public void setValue(AddrSpace spc, ulong off, int size, ulong cval)
        {
            MemoryBank* mspace = getMemoryBank(spc);
            if (mspace == (MemoryBank*)0)
                throw new LowlevelError("Setting value for unmapped memory space: " + spc.getName());
            mspace.setValue(off, size, cval);
        }

        /// Retrieve a memory value from the memory state
        /// This is the main interface for reading values from the MemoryState.
        /// If there is no registered MemoryBank for the desired address space, or
        /// if there is some other error, an exception is thrown.
        /// \param spc is the address space being queried
        /// \param off is the offset of the value being queried
        /// \param size is the number of bytes to query
        /// \return the queried value
        public ulong getValue(AddrSpace spc, ulong off, int size)
        {
            if (spc.getType() == spacetype.IPTR_CONSTANT) return off;
            MemoryBank* mspace = getMemoryBank(spc);
            if (mspace == (MemoryBank*)0)
                throw new LowlevelError("Getting value from unmapped memory space: " + spc.getName());
            return mspace.getValue(off, size);
        }

        /// Set a value on a named register in the memory state
        /// This is a convenience method for setting registers by name.
        /// Any register name known to the Translate object can be used as a write location.
        /// The associated address space, offset, and size is looked up and automatically
        /// passed to the main setValue routine.
        /// \param nm is the name of the register
        /// \param cval is the value to write to the register
        public void setValue(string nm,ulong cval)
        {
            // Set a "register" value
            VarnodeData vdata = trans.getRegister(nm);
            setValue(vdata.space, vdata.offset, vdata.size, cval);
        }

        /// Retrieve a value from a named register in the memory state
        /// This is a convenience method for reading registers by name.
        /// Any register name known to the Translate object can be used as a read location.
        /// The associated address space, offset, and size is looked up and automatically
        /// passed to the main getValue routine.
        /// \param nm is the name of the register
        /// \return the value associated with that register
        public ulong getValue(string nm)
        {
            // Get a "register" value
            VarnodeData vdata = trans.getRegister(nm);
            return getValue(vdata.space, vdata.offset, vdata.size);
        }

        /// Set value on a given \b varnode
        /// A convenience method for setting a value directly on a varnode rather than
        /// breaking out the components
        /// \param vn is a pointer to the varnode to be written
        /// \param cval is the value to write into the varnode
        public void setValue(VarnodeData vn, ulong cval)
        {
            setValue(vn.space, vn.offset, vn.size, cval);
        }

        /// Get a value from a \b varnode
        /// A convenience method for reading a value directly from a varnode rather
        /// than querying for the offset and space
        /// \param vn is a pointer to the varnode to be read
        /// \return the value read from the varnode
        public ulong getValue(VarnodeData vn) => getValue(vn.space, vn.offset, vn.size);

        /// Get a chunk of data from memory state
        /// This is the main interface for reading a range of bytes from the MemorySate.
        /// The MemoryBank associated with the address space of the query is looked up
        /// and the request is forwarded to the getChunk method on the MemoryBank. If there
        /// is no registered MemoryBank or some other error, an exception is thrown
        /// \param res is a pointer to the result buffer for storing retrieved bytes
        /// \param spc is the desired address space
        /// \param off is the starting offset of the byte range being queried
        /// \param size is the number of bytes being queried
        public void getChunk(byte[] res, AddrSpace spc, ulong off, int size)
        {
            MemoryBank* mspace = getMemoryBank(spc);
            if (mspace == (MemoryBank*)0)
                throw new LowlevelError("Getting chunk from unmapped memory space: " + spc.getName());
            mspace.getChunk(off, size, res);
        }

        /// Set a chunk of data from memory state
        /// This is the main interface for setting values for a range of bytes in the MemoryState.
        /// The MemoryBank associated with the desired address space is looked up and the
        /// write is forwarded to the setChunk method on the MemoryBank. If there is no
        /// registered MemoryBank or some other error, an exception  is throw.
        /// \param val is a pointer to the byte values to be written into the MemoryState
        /// \param spc is the address space being written
        /// \param off is the starting offset of the range being written
        /// \param size is the number of bytes to write
        public void setChunk(byte[] val, AddrSpace spc,ulong off, int size)
        {
            MemoryBank* mspace = getMemoryBank(spc);
            if (mspace == (MemoryBank*)0)
                throw new LowlevelError("Setting chunk of unmapped memory space: " + spc.getName());
            mspace.setChunk(off, size, val);
        }
    }
}
