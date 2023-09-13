using Sla.CORE;
using Sla.DECCORE;

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
            int delay = -1;

            name = s.ReadString();
            s.ReadSpaces();
            int delay;
            if (!int.TryParse(s.ReadString(), out delay)) delay = -1;

            AddrSpace? spc = dcp.conf.getSpaceByName(name);
            if (spc == (AddrSpace)null)
                throw new IfaceParseError("Bad space: " + name);
            if (delay == -1)
                throw new IfaceParseError("Need delay integer");
            if (dcp.fd != (Funcdata)null) {
                dcp.fd.getOverride().insertDeadcodeDelay(spc, delay);
                status.optr.WriteLine(
                    "Successfully overrided deadcode delay for single function");
            }
            else {
                dcp.conf.setDeadcodeDelay(spc, delay);
                status.optr.WriteLine(
                    "Successfully overrided deadcode delay for all functions");
            }
        }
    }
}
