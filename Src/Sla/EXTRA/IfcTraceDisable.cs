using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
#if OPACTION_DEBUG
    internal class IfcTraceDisable : IfaceDecompCommand
    {
        public override void execute(TextReader s)
{				// Turn off trace
  if (dcp.fd == (Funcdata *)0)
    throw IfaceExecutionError("No function selected");

  dcp.fd.debugDisable();
  *status.optr << "OK\n";
}
    }
#endif
}
