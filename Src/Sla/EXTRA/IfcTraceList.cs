using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
#if OPACTION_DEBUG
    internal class IfcTraceList : IfaceDecompCommand
    {
        public override void execute(TextReader s)
{				// List debug trace ranges
  int size,i;

  if (dcp.fd == (Funcdata *)0)
    throw IfaceExecutionError("No function selected");

  size = dcp.fd.debugSize();
  if (dcp.fd.opactdbg_on)
    *status.optr << "Trace enabled (";
  else
    *status.optr << "Trace disabled (";
  *status.optr << dec << size << " total ranges)\n";
  for(i=0;i<size;++i)
    dcp.fd.debugPrintRange(i);
}
    }
#endif
}
