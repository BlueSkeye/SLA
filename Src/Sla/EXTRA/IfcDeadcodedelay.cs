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
    internal class IfcDeadcodedelay : IfaceDecompCommand
    {
        /// \class IfcDeadcodedelay
        /// \brief Change when dead code elimination starts: `deadcode delay <name> <delay>`
        ///
        /// An address space is selected by name, along with a pass number.
        /// Dead code elimination for Varnodes in that address space is changed to start
        /// during that pass.  If there is a \e current function, the delay is altered only for
        /// that function, otherwise the delay is set globally for all functions.
        public override void execute(TextReader s)
        {
            string name;
            int4 delay = -1;
            AddrSpace* spc;

            s >> name;
            s >> ws;
            s >> delay;

            spc = dcp->conf->getSpaceByName(name);
            if (spc == (AddrSpace*)0)
                throw IfaceParseError("Bad space: " + name);
            if (delay == -1)
                throw IfaceParseError("Need delay integer");
            if (dcp->fd != (Funcdata*)0)
            {
                dcp->fd->getOverride().insertDeadcodeDelay(spc, delay);
                *status->optr << "Successfully overrided deadcode delay for single function" << endl;
            }
            else
            {
                dcp->conf->setDeadcodeDelay(spc, delay);
                *status->optr << "Successfully overrided deadcode delay for all functions" << endl;
            }
        }
    }
}
