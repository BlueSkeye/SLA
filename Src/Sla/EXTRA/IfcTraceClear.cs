using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
#if OPACTION_DEBUG
    internal class IfcTraceClear : IfaceDecompCommand
    {
        public override void execute(TextReader s)
{				// Clear existing debug trace ranges
  if (dcp.fd == (Funcdata *)0)
    throw IfaceExecutionError("No function selected");

  *status.optr << dec << dcp.fd.debugSize() << " ranges cleared\n";
  dcp.fd.debugDisable();
  dcp.fd.debugClear();
}
    }
#endif
}
