using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    internal abstract class TypeModifier
    {
        public enum Modifier
        {
            pointer_mod,
            array_mod,
            function_mod,
            struct_mod,
            enum_mod
        }

        ~TypeModifier()
        {
        }

        public abstract TypeModifier.Modifier getType();

        public abstract bool isValid();

        public abstract Datatype modType(Datatype? @base, TypeDeclarator decl, Architecture glb);
    }
}
