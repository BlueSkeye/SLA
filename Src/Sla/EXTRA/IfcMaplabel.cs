﻿using Sla.CORE;
using Sla.DECCORE;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    internal class IfcMaplabel : IfaceDecompCommand
    {
        /// \class IfcMaplabel
        /// \brief Create a code label: `map label <name> <address>`
        ///
        /// Label a specific code address.  This creates a LabSymbol which is usually
        /// an internal control-flow target.  The symbol is local to the \e current function
        /// if it exists, otherwise the symbol is added to the global scope.
        public override void execute(TextReader s)
        {
            string name = s.ReadString();
            if (name.Length == 0)
                throw new IfaceParseError("Need label name and address");
            int size;
            Address addr = Grammar.parse_machaddr(s, out size, dcp.conf.types); // Read address

            Scope scope;
            if (dcp.fd != (Funcdata)null)
                scope = dcp.fd.getScopeLocal();
            else
                scope = dcp.conf.symboltab.getGlobalScope();

            Symbol sym = scope.addCodeLabel(addr, name);
            scope.setAttribute(sym, Varnode.varnode_flags.namelock | Varnode.varnode_flags.typelock);
        }
    }
}
