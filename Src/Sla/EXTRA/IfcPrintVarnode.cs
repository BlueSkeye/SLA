using Sla.DECCORE;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    internal class IfcPrintVarnode : IfaceDecompCommand
    {
        /// \class IfcPrintVarnode
        /// \brief Print information about a Varnode: `print varnode <varnode>`
        ///
        /// Attributes of the indicated Varnode from the \e current function are printed
        /// to the console.  If the Varnode belongs to a HighVariable, information about
        /// it and all its Varnodes are printed as well.
        public override void execute(TextReader s)
        {
            Varnode vn;

            vn = dcp.readVarnode(s);
            if (vn.isAnnotation() || (!dcp.fd.isHighOn()))
                vn.printInfo(*status.optr);
            else
                vn.getHigh().printInfo(*status.optr);
        }
    }
}
