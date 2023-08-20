using Sla.DECCORE;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    internal class IfcVarnodeCover : IfaceDecompCommand
    {
        /// \class IfcVarnodeCover
        /// \brief Print cover information about a Varnode: `print cover varnode <varnode>`
        ///
        /// Information about code ranges where the single Varnode is in scope are printed.
        public override void execute(TextReader s)
        {
            Varnode vn;

            vn = dcp.readVarnode(s);
            if (vn == (Varnode)null)
                throw new IfaceParseError("Unknown varnode");
            vn.printCover(*status.optr);
        }
    }
}
