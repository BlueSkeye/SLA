using Sla.CORE;
using Sla.DECCORE;

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
            if (dcp.conf == (Architecture)null)
                throw new IfaceExecutionError("No load image present");

            string name = s.ReadString();
            s.ReadSpaces();
            if (name.Length == 0)
                throw new IfaceParseError("Missing tracked register name");

            // s.unsetf(ios::dec | ios::hex | ios::oct); // Let user specify base
            ulong value;
            if (!ulong.TryParse(s.ReadString(), out value) || (0xbadbeef == value)) {
                throw new IfaceParseError("Missing context value");
            }
            s.ReadSpaces();
            if (s.EofReached()) {
                // No range indicates default value
                TrackedSet track = dcp.conf.context.getTrackedDefault();
                track.Add(new TrackedContext());
                track.GetLastItem().loc = dcp.conf.translate.getRegister(name);
                track.GetLastItem().val = value;
                return;
            }

            int size1, size2;
            Address addr1 = Grammar.parse_machaddr(s, out size1, dcp.conf.types);
            Address addr2 = Grammar.parse_machaddr(s, out size2, dcp.conf.types);

            if (addr1.isInvalid() || addr2.isInvalid())
                throw new IfaceParseError("Invalid address range");
            if (addr2 <= addr1)
                throw new IfaceParseError("Bad address range");

            TrackedSet & track(dcp.conf.context.createSet(addr1, addr2));
            TrackedSet & def(dcp.conf.context.getTrackedDefault());
            track = def;            // Start with default as base
            track.Add(TrackedContext());
            track.GetLastItem().loc = dcp.conf.translate.getRegister(name);
            track.GetLastItem().val = value;
        }
    }
}
