﻿using Sla.DECCORE;

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

            if (dcp.conf != (Architecture)null)
                throw new IfaceExecutionError("Load image already present");
            string filename = s.ReadString();
            dcp.testCollection = new FunctionTestCollection(status);
            dcp.testCollection.loadTest(filename);
#if OPACTION_DEBUG
            dcp.conf.setDebugStream(status.fileoptr);
#endif
            status.optr.WriteLine($"{filename} test successfully loaded: {dcp.conf.getDescription()}");
        }
    }
}
