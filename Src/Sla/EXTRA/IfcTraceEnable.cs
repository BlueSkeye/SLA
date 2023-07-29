using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
#if OPACTION_DEBUG
    internal class IfcTraceEnable : IfaceDecompCommand
    {
        public override void execute(TextReader s)
{				// Turn on trace
  if (dcp.fd == (Funcdata *)0)
    throw new IfaceExecutionError("No function selected");

  dcp.fd.debugEnable();
  *status.optr << "OK\n";
}
    }
#endif
}
