using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
            Datatype* dt;

            string typeName;
            s >> ws >> typeName;
            dt = dcp.conf.types.findByName(typeName);
            if (dt == (Datatype)null)
                throw new IfaceExecutionError("Unknown data-type: " + typeName);
            string formatString;
            s >> ws >> formatString;
            uint format = Datatype.encodeIntegerFormat(formatString);
            dcp.conf.types.setDisplayFormat(dt, format);
            *status.optr << "Successfully forced data-type display" << endl;
        }
    }
}
