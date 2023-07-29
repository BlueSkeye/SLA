using Sla.CORE;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    internal class ImportRecord
    {
        internal string dllname;
        internal string funcname;
        internal int ordinal;
        internal Address address;
        internal Address thunkaddress;
    }
}
