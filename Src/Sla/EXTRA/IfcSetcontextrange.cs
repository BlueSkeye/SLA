﻿using Sla.CORE;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    internal class IfcSetcontextrange : IfaceDecompCommand
    {
        /// \class IfcSetcontextrange
        /// \brief Set a context variable: `set context <name> <value> [<startaddress> <endaddress>]`
        ///
        /// The named context variable is set to the provided value.
        /// If a start and end address is provided, the context variable is set over this range,
        /// otherwise the value is set as a default.
        public override void execute(TextReader s)
        {
            if (dcp.conf == (Architecture*)0)
                throw IfaceExecutionError("No load image present");

            string name;
            s >> name >> ws;

            if (name.size() == 0)
                throw IfaceParseError("Missing context variable name");

            s.unsetf(ios::dec | ios::hex | ios::oct); // Let user specify base
            uint value = 0xbadbeef;
            s >> value;
            if (value == 0xbadbeef)
                throw IfaceParseError("Missing context value");

            s >> ws;

            if (s.eof())
            {       // No range indicates default value
                dcp.conf.context.setVariableDefault(name, value);
                return;
            }

            // Otherwise parse the range
            int size1, size2;
            Address addr1 = parse_machaddr(s, size1, *dcp.conf.types); // Read begin address
            Address addr2 = parse_machaddr(s, size2, *dcp.conf.types); // Read end address

            if (addr1.isInvalid() || addr2.isInvalid())
                throw IfaceParseError("Invalid address range");
            if (addr2 <= addr1)
                throw IfaceParseError("Bad address range");

            dcp.conf.context.setVariableRegion(name, addr1, addr2, value);
        }
    }
}
