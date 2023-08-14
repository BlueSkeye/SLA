using Sla.CORE;
using Sla.DECCORE;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    internal class IfcProduceC : IfaceDecompCommand
    {
        /// \class IfcProduceC
        /// \brief Write decompilation for all functions to a file: `produce C <filename>`
        ///
        /// Iterate over all functions in the program.  For each function, decompilation is
        /// performed and output is appended to the file.
        public override void execute(TextReader s)
        {
            string name;

            s >> ws >> name;
            if (name.Length == 0)
                throw new IfaceParseError("Need file name to write to");

            using (StreamWriter os = new StreamWriter(File.OpenWrite(name.ToString()))) {
                dcp.conf.print.setOutputStream(os);
                iterateFunctionsAddrOrder();
            }
        }

        public override void iterationCallback(Funcdata fd)
        {
            DateTime start_time, end_time;

            if (fd.hasNoCode()) {
                status.optr.WriteLine("No code for {fd.getName()}");
                return;
            }
            try {
                dcp.conf.clearAnalysis(fd); // Clear any old analysis
                dcp.conf.allacts.getCurrent().reset(fd);
                start_time = DateTime.UtcNow();
                dcp.conf.allacts.getCurrent().perform(*fd);
                end_time = DateTime.UtcNow();
                status.optr.Write($"Decompiled {fd.getName()}");
                //	  *status.optr << ": " << hex << fd.getAddress().getOffset();
                status.optr.Write($"({fd.getSize()})");
                TimeSpan duration = (end_time - start_time);
                duration *= 1000.0;
                status.optr.WriteLine($" time={(int)duration.TotalMilliseconds} ms");
                dcp.conf.print.docFunction(fd);
            }
            catch (LowlevelError err) {
                status.optr.WriteLine($"Skipping {fd.getName()}: {err.ToString()}");
            }
            dcp.conf.clearAnalysis(fd);
        }
    }
}
