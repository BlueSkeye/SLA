
namespace Sla.EXTRA
{
    internal class IfcExecuteTestCommand : IfaceDecompCommand
    {
        /// \class IfcExecuteTestCommand
        /// \brief Execute a specified range of the test script: `execute test command <#>-<#>
        public override void execute(TextReader s)
        {
            if (dcp.testCollection == (FunctionTestCollection)null)
                throw new IfaceExecutionError("No test file is loaded");
            int first;
            int last;

            s.ReadSpaces();
            if (!int.TryParse(s.ReadString(), out first)) first = -1;
            first -= 1;
            if (first < 0 || first > dcp.testCollection.numCommands())
                throw new IfaceExecutionError("Command index out of bounds");
            s.ReadSpaces();
            if (!s.EofReached()) {
                s.ReadSpaces();
                char hyphen = s.ReadMandatoryCharacter();
                if (hyphen != '-')
                    throw new IfaceExecutionError("Missing hyphenated command range");
                s.ReadSpaces();
                if (!int.TryParse(s.ReadString(), out last)) last = -1;
                last -= 1;
                if (last < 0 || last < first || last > dcp.testCollection.numCommands())
                    throw new IfaceExecutionError("Command index out of bounds");
            }
            else {
                last = first;
            }
            TextWriter s1 = new StringWriter();
            for (int i = first; i <= last; ++i) {
                s1.WriteLine(dcp.testCollection.getCommand(i));
            }
            TextReader s2 = new StringReader(s1.ToString());
            status.pushScript(s2, "test> ");
        }
    }
}
