using Sla.DECCORE;

namespace Sla.EXTRA
{
    internal class IfcPrintCover : IfaceDecompCommand
    {
        /// \class IfcPrintCover
        /// \brief Print cover info about a HighVariable: `print cover high <name>`
        ///
        /// A HighVariable is specified by its symbol name in the current function's scope.
        /// Information about the code ranges where the HighVariable is in scope is printed.
        public override void execute(TextReader s)
        {
            HighVariable high;

            if (dcp.fd == (Funcdata)null)
                throw new IfaceExecutionError("No function selected");

            s.ReadSpaces();
            string name = s.ReadString();
            if (name.Length == 0)
                throw new IfaceParseError("Missing variable name");
            high = dcp.fd.findHigh(name);
            if (high == (HighVariable)null)
                throw new IfaceExecutionError("Unable to find variable: " + name);
            high.printCover(status.optr);
        }
    }
}
