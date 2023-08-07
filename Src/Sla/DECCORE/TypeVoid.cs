using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Sla.DECCORE
{
    /// \brief Formal "void" data-type object.
    ///
    /// A placeholder for "no data-type".
    /// This should be the only object with meta-type set to type_metatype.TYPE_VOID
    internal class TypeVoid : Datatype
    {
        // protected: friend class TypeFactory;

        /// Construct from another TypeVoid
        public TypeVoid(TypeVoid op)
                  : base(op)
        {
            flags |= Datatype.Properties.coretype;
        }
        
        /// Constructor
        public TypeVoid()
            : base(0, type_metatype.TYPE_VOID)
        {
            name = "void";
            displayName = name;
            flags |= Datatype.Properties.coretype;
        }
        
        internal override Datatype clone() => new TypeVoid(this);
    
        public override void encode(Encoder encoder)
        {
            if (typedefImm != (Datatype)null)
            {
                encodeTypedef(encoder);
                return;
            }
            encoder.openElement(ElementId.ELEM_VOID);
            encoder.closeElement(ElementId.ELEM_VOID);
        }
    }
}
