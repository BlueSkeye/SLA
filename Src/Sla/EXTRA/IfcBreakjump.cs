using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
#if OPACTION_DEBUG
    internal class IfcBreakJump : IfaceDecompCommand
    {
        public override void execute(TextReader s)
{
  dcp.jumptabledebug = true;
  dcp_callback = dcp;
  status_callback = status;
  *status.optr << "Jumptable debugging enabled" << endl;
  if (dcp.fd != (Funcdata *)0)
    dcp.fd.enableJTCallback(jump_callback);
}
    }
#endif
}
