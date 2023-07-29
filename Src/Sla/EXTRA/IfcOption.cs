using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    internal class IfcOption : IfaceDecompCommand
    {
        /// \class IfcOption
        /// \brief Adjust a decompiler option: `option <optionname> [<param1>] [<param2>] [<param3>]`
        ///
        /// Passes command-line parameters to an ArchOption object registered with
        /// the current architecture's OptionDatabase.  Options are looked up by name
        /// and can be configure with up to 3 parameters.  Options generally report success
        /// or failure back to the console.
        public override void execute(TextReader s)
        {
            string optname;
            string p1, p2, p3;

            if (dcp.conf == (Architecture*)0)
                throw IfaceExecutionError("No load image present");
            s >> ws >> optname >> ws;
            if (optname.size() == 0)
                throw IfaceParseError("Missing option name");
            if (!s.eof())
            {
                s >> p1 >> ws;
                if (!s.eof())
                {
                    s >> p2 >> ws;
                    if (!s.eof())
                    {
                        s >> p3 >> ws;
                        if (!s.eof())
                            throw IfaceParseError("Too many option parameters");
                    }
                }
            }

            try
            {
                string res = dcp.conf.options.set(ElementId::find(optname), p1, p2, p3);
                *status.optr << res << endl;
            }
            catch (ParseError err)
            {
                *status.optr << err.ToString() << endl;
                throw IfaceParseError("Bad option");
            }
            catch (RecovError err)
            {
                *status.optr << err.ToString() << endl;
                throw IfaceExecutionError("Bad option");
            }
        }
    }
}
