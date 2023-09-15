using Sla.CORE;
using Sla.DECCORE;
using System.Globalization;

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
            if (dcp.fd == (Funcdata)null) {
                throw new IfaceExecutionError("No function loaded");
            }
            ulong value;
            ulong hash;
            int size;
            Symbol.DisplayFlags format = 0;

            // Parse the format token
            string name = s.ReadString();
            if (name == "hex")
                format = Symbol.DisplayFlags.force_hex;
            else if (name == "dec")
                format = Symbol.DisplayFlags.force_dec;
            else if (name == "bin")
                format = Symbol.DisplayFlags.force_bin;
            else if (name == "oct")
                format = Symbol.DisplayFlags.force_oct;
            else if (name == "char")
                format = Symbol.DisplayFlags.force_char;
            else
                throw new IfaceParseError("Bad convert format");

            s.ReadSpaces();
            value = ulong.Parse(s.ReadString(), NumberStyles.HexNumber | NumberStyles.AllowHexSpecifier);
            // Read pc address of hash
            Address addr = Grammar.parse_machaddr(s, out size, dcp.conf.types);
            // Parse the hash value
            hash = ulong.Parse(s.ReadString(), NumberStyles.HexNumber | NumberStyles.AllowHexSpecifier);
            dcp.fd.getScopeLocal().addEquateSymbol("", format, value, addr, hash);
        }
    }
}
