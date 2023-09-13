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
            if (dcp.conf == (Architecture)null)
                throw new IfaceExecutionError("No load image present");

            string name = s.ReadString();
            s.ReadSpaces();

            if (name.Length == 0)
                throw new IfaceParseError("Missing context variable name");

            // s.unsetf(ios::dec | ios::hex | ios::oct); // Let user specify base
            uint value = 0xbadbeef;
            if (!uint.TryParse(s.ReadString(), out value) || value == 0xbadbeef)
                throw new IfaceParseError("Missing context value");

            s.ReadSpaces();

            if (s.EofReached()) {
                // No range indicates default value
                dcp.conf.context.setVariableDefault(name, value);
                return;
            }

            // Otherwise parse the range
            int size1, size2;
            Address addr1 = Grammar.parse_machaddr(s, out size1, dcp.conf.types); // Read begin address
            Address addr2 = Grammar.parse_machaddr(s, out size2, dcp.conf.types); // Read end address

            if (addr1.isInvalid() || addr2.isInvalid())
                throw new IfaceParseError("Invalid address range");
            if (addr2 <= addr1)
                throw new IfaceParseError("Bad address range");

            dcp.conf.context.setVariableRegion(name, addr1, addr2, value);
        }
    }
}
