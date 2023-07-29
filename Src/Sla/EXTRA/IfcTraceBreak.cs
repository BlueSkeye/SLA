using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
#if OPACTION_DEBUG
    internal class IfcTraceBreak : IfaceDecompCommand
    {
        public override void execute(TextReader s)
{				// Set a opactdbg trace break point
  int4 count;
  
  if (dcp.fd == (Funcdata *)0)
    throw IfaceExecutionError("No function selected");

  s >> ws;
  s.unsetf(ios::dec | ios::hex | ios::oct); // Let user specify base
  count = -1;
  s >> count;
  if (count == -1)
    throw IfaceParseError("Missing trace count");

  dcp.fd.debugSetBreak(count);
}
    }
#endif
}
