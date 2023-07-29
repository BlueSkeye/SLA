﻿using Sla.CORE;
using Sla.DECCORE;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    internal class IfcAddrrangeLoad : IfaceDecompCommand
    {
        /// \class IfcAddrrangeLoad
        /// \brief Create a new function at an address: `load addr <address> [<funcname>]`
        ///
        /// A new function is created at the provided address.  If a name is provided, this
        /// becomes the function symbol, otherwise a default name is generated.
        /// The function becomes \e current for the interface, and if bytes are present,
        /// raw p-code and control-flow are generated.
        public override void execute(TextReader s)
        {
            int4 size;
            string name;
            Address offset = parse_machaddr(s, size, *dcp->conf->types); // Read required address

            s >> ws;
            if (size <= offset.getAddrSize()) // Was a real size specified
                size = 0;
            if (dcp->conf->loader == (LoadImage*)0)
                throw IfaceExecutionError("No binary loaded");

            s >> name;          // Read optional name
            if (name.empty())
                dcp->conf->nameFunction(offset, name); // Pick default name if necessary
            dcp->fd = dcp->conf->symboltab->getGlobalScope()->addFunction(offset, name)->getFunction();
            dcp->followFlow(*status->optr, size);
        }
    }
}
