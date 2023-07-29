using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    internal class ArrayModifier : TypeModifier
    {
        private uint4 flags;
        private int4 arraysize;
        
        public ArrayModifier(uint4 fl, int4 @as)
        {
            flags = fl;
            arraysize = @as;
        }
        
        public override uint4 getType() => array_mod;
    
        public override bool isValid() => (arraysize>0);

        public override Datatype modType(Datatype @base, TypeDeclarator decl, Architecture glb)
        {
            Datatype* restype = glb->types->getTypeArray(arraysize, base);
            return restype;
        }
    }
}
