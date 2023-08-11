using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    internal struct TypeSpecifiers
    {
        internal Datatype type_specifier;
        internal string function_specifier;
        internal CParse.Flags flags;

        public TypeSpecifiers()
        {
            type_specifier = (Datatype)null;
            flags = 0;
        }
    }
}
