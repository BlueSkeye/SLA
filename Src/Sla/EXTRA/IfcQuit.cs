using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    internal class IfcQuit : IfaceBaseCommand
    {
        /// \class IfcQuit
        /// \brief Quit command to terminate processing from the given interface
        public override void execute(TextReader s)
        {
            // Generic quit call back
            if (-1 != s.Read()) {
                throw new IfaceParseError("Too many parameters to quit");
            }
            // Set flag to drop out of mainloop
            status.done = true;
        }
    }
}
