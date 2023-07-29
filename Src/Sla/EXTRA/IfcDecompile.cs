using Sla.DECCORE;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    internal class IfcDecompile : IfaceDecompCommand
    {
        /// \class IfcDecompile
        /// \brief Decompile the current function: `decompile`
        ///
        /// Decompilation is started for the current function. Any previous decompilation
        /// analysis on the function is cleared first.  The process respects
        /// any active break points or traces, so decompilation may not complete.
        public override void execute(TextReader s)
        {
            int4 res;

            if (dcp->fd == (Funcdata*)0)
                throw IfaceExecutionError("No function selected");

            if (dcp->fd->hasNoCode())
            {
                *status->optr << "No code for " << dcp->fd->getName() << endl;
                return;
            }
            if (dcp->fd->isProcStarted())
            { // Free up old decompile
                *status->optr << "Clearing old decompilation" << endl;
                dcp->conf->clearAnalysis(dcp->fd);
            }

            *status->optr << "Decompiling " << dcp->fd->getName() << endl;
            dcp->conf->allacts.getCurrent()->reset(*dcp->fd);
            res = dcp->conf->allacts.getCurrent()->perform(*dcp->fd);
            if (res < 0)
            {
                *status->optr << "Break at ";
                dcp->conf->allacts.getCurrent()->printState(*status->optr);
            }
            else
            {
                *status->optr << "Decompilation complete";
                if (res == 0)
                    *status->optr << " (no change)";
            }
            *status->optr << endl;
        }
    }
}
