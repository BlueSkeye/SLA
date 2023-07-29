using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    internal class IfcExecuteTestCommand : IfaceDecompCommand
    {
        /// \class IfcExecuteTestCommand
        /// \brief Execute a specified range of the test script: `execute test command <#>-<#>
        public override void execute(TextReader s)
        {
            if (dcp.testCollection == (FunctionTestCollection*)0)
                throw IfaceExecutionError("No test file is loaded");
            int4 first = -1;
            int4 last = -1;
            char hyphen;

            s >> ws >> dec >> first;
            first -= 1;
            if (first < 0 || first > dcp.testCollection.numCommands())
                throw IfaceExecutionError("Command index out of bounds");
            s >> ws;
            if (!s.eof())
            {
                s >> ws >> hyphen;
                if (hyphen != '-')
                    throw IfaceExecutionError("Missing hyphenated command range");
                s >> ws >> last;
                last -= 1;
                if (last < 0 || last < first || last > dcp.testCollection.numCommands())
                    throw IfaceExecutionError("Command index out of bounds");
            }
            else
            {
                last = first;
            }
            ostringstream s1;
            for (int4 i = first; i <= last; ++i)
            {
                s1 << dcp.testCollection.getCommand(i) << endl;
            }
            istringstream* s2 = new istringstream(s1.str());
            status.pushScript(s2, "test> ");
        }
    }
}
