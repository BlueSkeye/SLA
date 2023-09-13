using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
#if OPACTION_DEBUG
    internal class IfcTraceAddress : IfaceDecompCommand
    {
        public override void execute(TextReader s)
{				// Set a opactdbg trace point
  uint uqlow,uqhigh;
  int discard;

  if (dcp.fd == (Funcdata *)0)
    throw new IfaceExecutionError("No function selected");

  Address pclow,pchigh;
  s.ReadSpaces();
  if (!s.EofReached()) {
    pclow = Grammar.parse_machaddr(s, out discard, dcp.conf.types);
    s.ReadSpaces();
  }
  pchigh = pclow;
  if (!s.EofReached()) {
    pchigh = Grammar.parse_machaddr(s, out discard, dcp.conf.types);
    s.ReadSpaces();
  }
  uqhigh = uqlow = uint.MaxValue;
  if (!s.EofReached()) {
    s.unsetf(ios::dec | ios::hex | ios::oct); // Let user specify base
    s >> uqlow >> uqhigh >> ws;
  }
  dcp.fd.debugSetRange(pclow,pchigh,uqlow,uqhigh);
  *status.optr << "OK (" << dec << dcp.fd.debugSize() << " ranges)\n";
}
    }
#endif
}
