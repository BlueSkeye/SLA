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
    throw IfaceExecutionError("No function selected");

  Address pclow,pchigh;
  s >> ws;
  if (!s.eof()) {
    pclow = parse_machaddr(s,discard,*dcp.conf.types);
    s >> ws;
  }
  pchigh = pclow;
  if (!s.eof()) {
    pchigh = parse_machaddr(s,discard,*dcp.conf.types);
    s >> ws;
  }
  uqhigh = uqlow = ~((uint)0);
  if (!s.eof()) {
    s.unsetf(ios::dec | ios::hex | ios::oct); // Let user specify base
    s >> uqlow >> uqhigh >> ws;
  }
  dcp.fd.debugSetRange(pclow,pchigh,uqlow,uqhigh);
  *status.optr << "OK (" << dec << dcp.fd.debugSize() << " ranges)\n";
}
    }
#endif
}
