using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    internal class IfcEcho : IfaceBaseCommand
    {
        /// \class IfcEcho
        /// \brief Echo command to echo the current command line to the bulk output stream
        public override void execute(TextReader s)
        {               // Echo command line to fileoptr
            char c;

            while (s.get(c))
                status.fileoptr.put(c);
            *status.fileoptr << endl;
        }
    }
}
