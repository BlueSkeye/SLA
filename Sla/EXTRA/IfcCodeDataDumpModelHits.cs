using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    internal class IfcCodeDataDumpModelHits : IfaceCodeDataCommand
    {
        public override void execute(istream s)
        {
            codedata->dumpModelHits(*status->fileoptr);
        }
    }
}
