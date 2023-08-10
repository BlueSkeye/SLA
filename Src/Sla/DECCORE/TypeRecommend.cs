using Sla.CORE;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief Data-type for a storage location when there is no Symbol (yet)
    ///
    /// Allow a data-type to be fed into a specific storage location.  Currently
    /// this only applies to input Varnodes.
    internal class TypeRecommend
    {
        /// Storage address of the Varnode
        private Address addr;
        /// Data-type to assign to the Varnode
        private Datatype dataType;
        
        public TypeRecommend(Address ad, Datatype dt)
        {
            addr = ad;
            dataType = dt;
        }

        /// Get the storage address
        public Address getAddress() => addr;

        /// Get the data-type
        public Datatype getType() => dataType;
    }
}
