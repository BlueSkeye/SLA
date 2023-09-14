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
        private uint flags;
        private int arraysize;
        
        public ArrayModifier(uint fl, int @as)
        {
            flags = fl;
            arraysize = @as;
        }
        
        public override Modifier getType() => Modifier.array_mod;
    
        public override bool isValid() => (arraysize>0);

        public override Datatype modType(Datatype? @base, TypeDeclarator decl, Architecture glb)
        {
            Datatype restype = glb.types.getTypeArray(arraysize, @base);
            return restype;
        }
    }
}
