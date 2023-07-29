using Sla.DECCORE;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    internal class IfcReadSymbols : IfaceDecompCommand
    {
        /// \class IfcReadSymbols
        /// \brief Read in symbols from the load image: `read symbols`
        ///
        /// If the load image format encodes symbol information.  These are
        /// read in and attached to the appropriate address.
        public override void execute(TextReader s)
        {
            if (dcp.conf == (Architecture*)0)
                throw IfaceExecutionError("No load image present");
            if (dcp.conf.loader == (LoadImage*)0)
                throw IfaceExecutionError("No binary loaded");

            dcp.conf.readLoaderSymbols("::");
        }
    }
}
