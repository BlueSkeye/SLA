using ghidra;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief A name recommendation for a particular dynamic storage location
    ///
    /// A recommendation for a symbol name whose storage is dynamic. The storage
    /// is identified using the DynamicHash mechanism and may or may not exist.
    internal class DynamicRecommend
    {
        /// Use point of the Symbol
        private Address usePoint;
        /// Hash encoding the Symbols environment
        private uint8 hash;
        /// The local symbol name recommendation
        private string name;
        /// Id associated with the original Symbol
        private uint8 symbolId;
        
        public DynamicRecommend(Address addr,uint8 h, string nm,uint8 id)
        {
            usePoint = addr;
            hash = h;
            name = nm;
            symbolId = id;
        }

        /// Get the use point address
        public Address getAddress() => usePoint;

        /// Get the dynamic hash
        public uint8 getHash() => hash;

        /// Get the recommended name
        public string getName() => name;

        /// Get the original Symbol id
        public uint8 getSymbolId() => symbolId;
    }
}
