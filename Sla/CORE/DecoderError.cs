using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.CORE
{
    /// \brief An exception thrown by the XML parser
    /// This object holds the error message as passed to the SAX interface callback
    /// and is thrown as a formal exception.
    internal class DecoderError : Exception
    {
        ///< Constructor
        public DecoderError(string s)
            : base(s)
        {
        }
    }
}
