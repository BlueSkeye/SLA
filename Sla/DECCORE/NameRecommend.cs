using ghidra;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Intrinsics.X86;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief A symbol name recommendation with its associated storage location
    ///
    /// The name is associated with a static Address and use point in the code. Symbols
    /// present at the end of function decompilation without a name can acquire \b this name
    /// if their storage matches.
    internal class NameRecommend
    {
        /// The starting address of the storage location
        private Address addr;
        /// The code address at the point of use
        private Address useaddr;
        /// An optional/recommended size for the variable being stored
        private int4 size;
        /// The local symbol name recommendation
        private string name;
        /// Id associated with the original Symbol
        private uint8 symbolId;
        
        public NameRecommend(Address ad, Address use,int4 sz, string nm,uint8 id)
        {
            addr = ad;
            useaddr = use;
            size = sz;
            name = nm;
            symbolId = id;
        }

        /// Get the storage address
        public Address getAddr() => addr;

        /// Get the use point address
        public Address getUseAddr() => useaddr;

        /// Get the optional size
        public int4 getSize() => size;

        /// Get the recommended name
        public string getName() => name;

        /// Get the original Symbol id
        public uint8 getSymbolId() => symbolId;
    }
}
