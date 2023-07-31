using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    internal class IfcBreakstart : IfaceDecompCommand
    {
        /// \class IfcBreakstart
        /// \brief Set a break point at the start of an Action: `break start <actionname>`
        ///
        /// The break point can be on either an Action or a Rule.  The name can specify
        /// partial path information to distinguish the Action/Rule.  The breakpoint causes
        /// the decompilation process to stop and return control to the console just before
        /// the Action/Rule would have executed.
        public override void execute(TextReader s)
        {
            bool res;
            string specify;

            s >> specify >> ws;     // Which action or rule to put breakpoint on

            if (specify.empty())
                throw new IfaceExecutionError("No action/rule specified");

            if (dcp.conf == (Architecture)null)
                throw new IfaceExecutionError("Decompile action not loaded");

            res = dcp.conf.allacts.getCurrent().setBreakPoint(Action::break_start, specify);
            if (!res)
                throw new IfaceExecutionError("Bad action/rule specifier: " + specify);
        }
    }
}
