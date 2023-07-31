using Sla.DECCORE;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    internal class IfcCountPcode : IfaceDecompCommand
    {
        /// \class IfcCountPcode
        /// \brief Count p-code in the \e current function: `count pcode`
        ///
        /// The count is based on the number of existing p-code operations in
        /// the current function, which may vary depending on the state of it transformation.
        public override void execute(TextReader s)
        {
            if (dcp.conf == (Architecture)null)
                throw new IfaceExecutionError("Image not loaded");

            if (dcp.fd == (Funcdata)null)
                throw new IfaceExecutionError("No function selected");

            uint count = 0;
            list<PcodeOp*>::const_iterator iter, enditer;
            iter = dcp.fd.beginOpAlive();
            enditer = dcp.fd.endOpAlive();
            while (iter != enditer)
            {
                count += 1;
                ++iter;
            }
            *status.optr << "Count - pcode = " << dec << count << endl;
        }
    }
}
