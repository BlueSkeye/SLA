using Sla.CORE;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    internal class IfcSettrackedrange : IfaceDecompCommand
    {
        /// \class IfcSettrackedrange
        /// \brief Set the value of a register: `set track <name> <value> [<startaddress> <endaddress>]`
        ///
        /// The value for the register is picked up by the decompiler for functions in the tracked range.
        /// The register is specified by name.  A specific range can be provided, otherwise the value is
        /// treated as a default.
        public override void execute(TextReader s)
        {
            if (dcp->conf == (Architecture*)0)
                throw IfaceExecutionError("No load image present");

            string name;
            s >> name >> ws;
            if (name.size() == 0)
                throw IfaceParseError("Missing tracked register name");

            s.unsetf(ios::dec | ios::hex | ios::oct); // Let user specify base
            uintb value = 0xbadbeef;
            s >> value;
            if (value == 0xbadbeef)
                throw IfaceParseError("Missing context value");

            s >> ws;
            if (s.eof())
            {       // No range indicates default value
                TrackedSet & track(dcp->conf->context->getTrackedDefault());
                track.push_back(TrackedContext());
                track.back().loc = dcp->conf->translate->getRegister(name);
                track.back().val = value;
                return;
            }

            int4 size1, size2;
            Address addr1 = parse_machaddr(s, size1, *dcp->conf->types);
            Address addr2 = parse_machaddr(s, size2, *dcp->conf->types);

            if (addr1.isInvalid() || addr2.isInvalid())
                throw IfaceParseError("Invalid address range");
            if (addr2 <= addr1)
                throw IfaceParseError("Bad address range");

            TrackedSet & track(dcp->conf->context->createSet(addr1, addr2));
            TrackedSet & def(dcp->conf->context->getTrackedDefault());
            track = def;            // Start with default as base
            track.push_back(TrackedContext());
            track.back().loc = dcp->conf->translate->getRegister(name);
            track.back().val = value;
        }
    }
}
