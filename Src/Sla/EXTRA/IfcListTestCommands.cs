using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    internal class IfcListTestCommands : IfaceDecompCommand
    {
        /// \class IfcListTestCommands
        /// \brief List all the script commands in the current test: `list test commands`
        public override void execute(TextReader s)
        {
            if (dcp.testCollection == (FunctionTestCollection)null)
                throw new IfaceExecutionError("No test file is loaded");
            for (int i = 0; i < dcp.testCollection.numCommands(); ++i)
            {
                *status.optr << ' ' << dec << i + 1 << ": " << dcp.testCollection.getCommand(i) << endl;
            }
        }
    }
}
