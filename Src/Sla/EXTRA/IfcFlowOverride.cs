using Sla.CORE;
using Sla.DECCORE;

namespace Sla.EXTRA
{
    internal class IfcFlowOverride : IfaceDecompCommand
    {
        /// \class IfcFlowOverride
        /// \brief Create a control-flow override: `override flow <address> branch|call|callreturn|return`
        ///
        /// Change the nature of the control-flow at the specified address, as indicated by the
        /// final token on the command-line:
        ///   - branch     -  Change the CALL or RETURN to a BRANCH
        ///   - call       -  Change a BRANCH or RETURN to a CALL
        ///   - callreturn -  Change a BRANCH or RETURN to a CALL followed by a RETURN
        ///   - return     -  Change a CALLIND or BRANCHIND to a RETURN
        public override void execute(TextReader s)
        {
            int discard;

            if (dcp.fd == (Funcdata)null)
                throw new IfaceExecutionError("No function selected");

            s.ReadSpaces();
            Address addr = Grammar.parse_machaddr(s, out discard, dcp.conf.types);
            string token = s.ReadString();
            if (token.Length == 0)
                throw new IfaceParseError("Missing override type");
            Override.Branching type = Override.stringToType(token);
            if (type == Override.Branching.NONE)
                throw new IfaceParseError("Bad override type");

            dcp.fd.getOverride().insertFlowOverride(addr, type);
            status.optr.WriteLine("Successfully added override");
        }
    }
}
