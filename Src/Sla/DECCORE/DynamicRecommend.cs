using Sla.CORE;
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
        private ulong hash;
        /// The local symbol name recommendation
        private string name;
        /// Id associated with the original Symbol
        private ulong symbolId;
        
        public DynamicRecommend(Address addr,ulong h, string nm,ulong id)
        {
            usePoint = addr;
            hash = h;
            name = nm;
            symbolId = id;
        }

        /// Get the use point address
        public Address getAddress() => usePoint;

        /// Get the dynamic hash
        public ulong getHash() => hash;

        /// Get the recommended name
        public string getName() => name;

        /// Get the original Symbol id
        public ulong getSymbolId() => symbolId;
    }
}
