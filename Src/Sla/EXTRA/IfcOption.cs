using Sla.CORE;
using Sla.DECCORE;

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
            if (dcp.conf == (Architecture)null)
                throw new IfaceExecutionError("No load image present");
            s.ReadSpaces();
            string optname = s.ReadString();
            s.ReadSpaces();
            if (optname.Length == 0)
                throw new IfaceParseError("Missing option name");
            string p1 = string.Empty;
            string p2 = string.Empty;
            string p3 = string.Empty;
            if (!s.EofReached()) {
                p1 = s.ReadString();
                s.ReadSpaces();
                if (!s.EofReached()) {
                    p2 = s.ReadString();
                    s.ReadSpaces();
                    if (!s.EofReached()) {
                        p3 = s.ReadString();
                        s.ReadSpaces();
                        if (!s.EofReached())
                            throw new IfaceParseError("Too many option parameters");
                    }
                }
            }

            try {
                string res = dcp.conf.options.set(ElementId.find(optname), p1, p2, p3);
                status.optr.WriteLine(res);
            }
            catch (ParseError err) {
                status.optr.WriteLine(err.ToString());
                throw new IfaceParseError("Bad option");
            }
            catch (RecovError err) {
                status.optr.WriteLine(err.ToString());
                throw new IfaceExecutionError("Bad option");
            }
        }
    }
}
