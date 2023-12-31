﻿using Sla.CORE;
using Sla.DECCORE;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    internal class IfcMaphash : IfaceDecompCommand
    {
        /// \class IfcMaphash
        /// \brief Add a dynamic symbol to the current function: `map hash <address> <hash> <typedeclaration>`
        ///
        /// The command only creates local variables for the current function.
        /// The name and data-type are taken from a C syntax type declaration.  The symbol is
        /// not associated with a particular storage address but with a specific Varnode in the data-flow,
        /// specified by a code address and hash of the local data-flow structure.
        public override void execute(TextReader s)
        {
            if (dcp.fd == (Funcdata)null) {
                throw new IfaceExecutionError("No function loaded");
            }
            string name;
            int size;
            // Read pc address of hash
            Address addr = Grammar.parse_machaddr(s, out size, dcp.conf.types);

            // Parse the hash value
            ulong hash = ulong.Parse(s.ReadString(),
                NumberStyles.HexNumber | NumberStyles.AllowHexSpecifier);
            s.ReadSpaces();
            // Parse the required type and name
            Datatype ct = Grammar.parse_type(s, out name, dcp.conf);

            Symbol sym = dcp.fd.getScopeLocal().addDynamicSymbol(name, ct, addr, hash);
            sym.getScope().setAttribute(sym,
                Varnode.varnode_flags.namelock | Varnode.varnode_flags.typelock);
        }
    }
}
