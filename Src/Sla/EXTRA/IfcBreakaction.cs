using Sla.DECCORE;

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
            // Which action or rule to put breakpoint on
            string specify = s.ReadString();
            s.ReadSpaces();

            if (specify.empty())
                throw new IfaceExecutionError("No action/rule specified");
            if (dcp.conf == (Architecture)null)
                throw new IfaceExecutionError("Decompile action not loaded");
            bool res = dcp.conf.allacts.getCurrent().setBreakPoint(
                Sla.DECCORE.Action.breakflags.break_action, specify);
            if (!res)
                throw new IfaceExecutionError("Bad action/rule specifier: " + specify);
        }
    }
}
