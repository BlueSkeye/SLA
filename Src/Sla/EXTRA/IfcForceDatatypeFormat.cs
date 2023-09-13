using Sla.DECCORE;

namespace Sla.EXTRA
{
    internal class IfcForceDatatypeFormat : IfaceDecompCommand
    {
        /// \class IfcForceDatatypeFormat
        /// \brief Mark constants of a data-type to be printed in a specific format: `force datatype <datatype> [hex|dec|oct|bin|char]`
        ///
        /// A display format attribute is set on the indicated data-type.
        public override void execute(TextReader s)
        {
            string typeName;
            s.ReadSpaces();
            typeName = s.ReadString();
            Datatype? dt = dcp.conf.types.findByName(typeName);
            if (dt == (Datatype)null)
                throw new IfaceExecutionError("Unknown data-type: " + typeName);
            s.ReadSpaces();
            string formatString = s.ReadString();
            uint format = Datatype.encodeIntegerFormat(formatString);
            dcp.conf.types.setDisplayFormat(dt, format);
            status.optr.WriteLine("Successfully forced data-type display");
        }
    }
}
