using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
#if CPUI_RULECOMPILE
    internal class IfcExperimentalRules : IfaceDecompCommand
    {
        public override void execute(TextReader s)
{
  string filename;

  if (dcp->conf != (Architecture *)0)
    throw IfaceExecutionError("Experimental rules must be registered before loading architecture");
  s >> filename;
  if (filename.size() == 0)
    throw IfaceParseError("Missing name of file containing experimental rules");
  dcp->experimental_file = filename;
  *status->optr << "Successfully registered experimental file " << filename << endl;
}
    }
#endif
}
