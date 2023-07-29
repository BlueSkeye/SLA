using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    internal class IfcCodeDataDumpCrossRefs : IfaceCodeDataCommand
    {
        public override void execute(istream s)
        {
            codedata->dumpCrossRefs(*status->fileoptr);
        }
    }
}
