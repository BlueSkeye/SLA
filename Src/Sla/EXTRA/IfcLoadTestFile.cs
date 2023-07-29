using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    internal class IfcLoadTestFile : IfaceDecompCommand
    {
        /// \class IfcLoadTestFile
        /// \brief Load a datatest environment file: `load test <filename>`
        ///
        /// The program and associated script from a decompiler test file is loaded
        public override void execute(TextReader s)
        {
            string filename;

            if (dcp.conf != (Architecture*)0)
                throw new IfaceExecutionError("Load image already present");
            s >> filename;
            dcp.testCollection = new FunctionTestCollection(status);
            dcp.testCollection.loadTest(filename);
#if OPACTION_DEBUG
            dcp.conf.setDebugStream(status.fileoptr);
#endif
            *status.optr << filename << " test successfully loaded: " << dcp.conf.getDescription() << endl;
        }
    }
}
