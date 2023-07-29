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
            if (name.size() == 0)
                throw IfaceParseError("Need file name to write to");

            ofstream os;
            os.open(name.c_str());
            dcp->conf->print->setOutputStream(&os);

            iterateFunctionsAddrOrder();

            os.close();
        }

        public override void iterationCallback(Funcdata fd)
        {
            clock_t start_time, end_time;
            float duration;

            if (fd->hasNoCode())
            {
                *status->optr << "No code for " << fd->getName() << endl;
                return;
            }
            try
            {
                dcp->conf->clearAnalysis(fd); // Clear any old analysis
                dcp->conf->allacts.getCurrent()->reset(*fd);
                start_time = clock();
                dcp->conf->allacts.getCurrent()->perform(*fd);
                end_time = clock();
                *status->optr << "Decompiled " << fd->getName();
                //	  *status->optr << ": " << hex << fd->getAddress().getOffset();
                *status->optr << '(' << dec << fd->getSize() << ')';
                duration = ((float)(end_time - start_time)) / CLOCKS_PER_SEC;
                duration *= 1000.0;
                *status->optr << " time=" << fixed << setprecision(0) << duration << " ms" << endl;
                dcp->conf->print->docFunction(fd);
            }
            catch (LowlevelError err) {
                *status->optr << "Skipping " << fd->getName() << ": " << err.explain << endl;
            }
            dcp->conf->clearAnalysis(fd);
        }
    }
}
