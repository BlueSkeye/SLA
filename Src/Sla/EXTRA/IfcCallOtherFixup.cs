using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    internal class IfcCallOtherFixup : IfaceDecompCommand
    {
        /// \class IfcCallOtherFixup
        /// \brief Add a new callother fix-up to the program: `fixup callother ...`
        ///
        /// The new fix-up is suitable for replacing specific user-defined (CALLOTHER)
        /// p-code operations. The declarator provides the name of the fix-up and can also
        /// provide formal input and output parameters.
        /// \code
        ///   fixup callother outvar myfixup2(invar1,invar2) { outvar = invar1 + invar2; }
        /// \endcode
        public override void execute(TextReader s)
        {
            string useropname, outname, pcodestring;
            List<string> inname = new List<string>();

            IfcCallFixup.readPcodeSnippet(s, out useropname, out outname, inname, out pcodestring);
            dcp.conf.userops.manualCallOtherFixup(useropname, outname, inname, pcodestring, dcp.conf);

            status.optr.WriteLine("Successfully registered callotherfixup");
        }
    }
}
