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
  int count;
  
  if (dcp.fd == (Funcdata *)0)
    throw new IfaceExecutionError("No function selected");

  s.ReadSpaces();
  s.unsetf(ios::dec | ios::hex | ios::oct); // Let user specify base
  count = -1;
  s >> count;
  if (count == -1)
    throw new IfaceParseError("Missing trace count");

  dcp.fd.debugSetBreak(count);
}
    }
#endif
}
