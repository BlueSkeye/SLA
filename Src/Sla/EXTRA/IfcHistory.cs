using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    internal class IfcHistory : IfaceBaseCommand
    {
        /// \class IfcHistory
        /// \brief History command to list the most recent successful commands
        public override void execute(TextReader s)
        {
            // List most recent command lines
            int num;

            if (!s.EofReached()) {
                num = int.Parse(s.ReadString());
                s.ReadSpaces();
                if (!s.EofReached())
                    throw new IfaceParseError("Too many parameters to history");
            }
            else {
                // Default number of history lines
                num = 10;
            }

            if (num > status.getHistorySize())
                num = status.getHistorySize();

            for (int i = num - 1; i >= 0; --i) {
                string historyline;
                // List oldest to newest
                status.getHistory(out historyline, i);
                status.optr.WriteLine(historyline);
            }
        }
    }
}
