﻿using System;
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
        {               // List most recent command lines
            int4 num;
            string historyline;

            if (!s.eof())
            {
                s >> num >> ws;
                if (!s.eof())
                    throw IfaceParseError("Too many parameters to history");
            }
            else
                num = 10;           // Default number of history lines

            if (num > status->getHistorySize())
                num = status->getHistorySize();

            for (int4 i = num - 1; i >= 0; --i)
            {   // List oldest to newest
                status->getHistory(historyline, i);
                *status->optr << historyline << endl;
            }
        }
    }
}