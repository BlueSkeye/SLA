using Sla.DECCORE;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    internal class IfcVarnodehighCover : IfaceDecompCommand
    {
        /// \class IfcVarnodehighCover
        /// \brief Print cover info about a HighVariable: `print cover varnodehigh <varnode>`
        ///
        /// The HighVariable is selected by specifying one of its Varnodes.
        /// Information about the code ranges where the HighVariable is in scope is printed.
        public override void execute(TextReader s)
        {
            Varnode* vn;

            vn = dcp.readVarnode(s);
            if (vn == (Varnode)null)
                throw new IfaceParseError("Unknown varnode");
            if (vn.getHigh() != (HighVariable)null)
                vn.getHigh().printCover(*status.optr);
            else
                *status.optr << "Unmerged" << endl;
        }
    }
}
