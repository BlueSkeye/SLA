using Sla.CORE;
using Sla.DECCORE;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    internal class IfcMapconvert : IfaceDecompCommand
    {
        /// \class IfcMapconvert
        /// \brief Create an convert directive: `map convert <format> <value> <address> <hash>`
        ///
        /// Creates a \e convert directive that causes a targeted constant value to be displayed
        /// with the specified integer format.  The constant is specified by \e value, and the
        /// \e address of the p-code op using the constant plus a dynamic \e hash is also given.
        public override void execute(TextReader s)
        {
            if (dcp.fd == (Funcdata*)0)
                throw IfaceExecutionError("No function loaded");
            string name;
            ulong value;
            ulong hash;
            int size;
            uint format = 0;

            s >> name;      // Parse the format token
            if (name == "hex")
                format = Symbol::force_hex;
            else if (name == "dec")
                format = Symbol::force_dec;
            else if (name == "bin")
                format = Symbol::force_bin;
            else if (name == "oct")
                format = Symbol::force_oct;
            else if (name == "char")
                format = Symbol::force_char;
            else
                throw IfaceParseError("Bad convert format");

            s >> ws >> hex >> value;
            Address addr = parse_machaddr(s, size, *dcp.conf.types); // Read pc address of hash

            s >> hex >> hash;       // Parse the hash value

            dcp.fd.getScopeLocal().addEquateSymbol("", format, value, addr, hash);
        }
    }
}
