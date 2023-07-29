using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    internal class IfcCodeDataDumpStarts : IfaceCodeDataCommand
    {
        public override void execute(istream s)
        {
            codedata.dumpFunctionStarts(*status.fileoptr);
        }
    }
}
