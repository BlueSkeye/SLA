using Sla.DECCORE;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Sla.DECCORE
{
    /// \brief Base class for the fundamental atomic types.
    ///
    /// Data-types with a name, size, and meta-type
    internal class TypeBase : Datatype
    {
        // friend class TypeFactory;
        /// Construct TypeBase copying properties from another data-type
        public TypeBase(TypeBase op)
            : base(op)
        {
        }

        /// Construct TypeBase from a size and meta-type
        public TypeBase(int s, type_metatype m)
            : base(s, m)
        {
        }

        /// Construct TypeBase from a size, meta-type, and name
        public TypeBase(int s, type_metatype m, string n)
            : base(s, m)
        {
            name = n;
            displayName = n;
        }
        
        public override Datatype clone() => new TypeBase(this);
    }
}
