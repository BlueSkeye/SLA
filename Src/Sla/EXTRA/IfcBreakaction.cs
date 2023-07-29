using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    internal class IfcBreakaction : IfaceDecompCommand
    {
        /// \class IfcBreakaction
        /// \brief Set a breakpoint when a Rule or Action executes: `break action <actionname>`
        ///
        /// The break point can be on either an Action or Rule.  The name can specify
        /// partial path information to distinguish the Action/Rule.  The breakpoint causes
        /// the decompilation process to stop and return control to the console immediately
        /// \e after the Action or Rule has executed, but only if there was an active transformation
        /// to the function.
        public override void execute(TextReader s)
        {
            bool res;
            string specify;

            s >> specify >> ws;     // Which action or rule to put breakpoint on

            if (specify.empty())
                throw IfaceExecutionError("No action/rule specified");

            if (dcp.conf == (Architecture*)0)
                throw IfaceExecutionError("Decompile action not loaded");

            res = dcp.conf.allacts.getCurrent().setBreakPoint(Action::break_action, specify);
            if (!res)
                throw IfaceExecutionError("Bad action/rule specifier: " + specify);
        }
    }
}
