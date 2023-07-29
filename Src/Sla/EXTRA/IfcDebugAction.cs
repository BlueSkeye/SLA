using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
#if OPACTION_DEBUG
    internal class IfcDebugAction : IfaceDecompCommand
    {
        public override void execute(TextReader s)
{
  if (dcp->fd == (Funcdata *)0)
    throw IfaceExecutionError("No function selected");
  string actionname;
  s >> ws >> actionname;
  if (actionname.empty())
    throw IfaceParseError("Missing name of action to debug");
  if (!dcp->conf->allacts.getCurrent()->turnOnDebug(actionname))
    throw IfaceParseError("Unable to find action "+actionname);
}
    }
#endif
}
