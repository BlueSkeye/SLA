using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    internal class IfcComment : IfaceDecompCommand
    {
        /// \class IfcComment
        /// \brief A comment within a command script: `% A comment in a script`
        ///
        /// This commands does nothing but attaches to comment tokens like:
        ///   - \#
        ///   - %
        ///   - //
        ///
        /// allowing comment lines in a script file
        public override void execute(TextReader s)
        {
            //Do nothing
        }
    }
}
