using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    internal class PointerModifier : TypeModifier
    {
        private uint4 flags;
        
        public PointerModifier(uint4 fl)
        {
            flags = fl;
        }
        
        public override uint4 getType() => pointer_mod;

        public override bool isValid() => true;

        public override Datatype modType(Datatype @base, TypeDeclarator decl, Architecture glb)
        {
            int4 addrsize = glb->getDefaultDataSpace()->getAddrSize();
            Datatype* restype;
            restype = glb->types->getTypePointer(addrsize, @base,
                glb->getDefaultDataSpace()->getWordSize());
            return restype;
        }
    }
}
