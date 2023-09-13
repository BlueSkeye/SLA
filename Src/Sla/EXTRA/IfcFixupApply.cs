using Sla.DECCORE;

namespace Sla.EXTRA
{
    internal class IfcFixupApply : IfaceDecompCommand
    {
        /// \class IfcFixupApply
        /// \brief Apply a call-fixup to a particular function: `fixup apply <fixup> <function>`
        ///
        /// The call-fixup and function are named from the command-line. If they both exist,
        /// the fixup is set on the function's prototype.
        public override void execute(TextReader s)
        {
            if (dcp.conf == (Architecture)null)
                throw new IfaceExecutionError("No load image present");

            s.ReadSpaces();
            if (s.EofReached())
                throw new IfaceParseError("Missing fixup name");
            string fixupName = s.ReadString();
            s.ReadSpaces();
            if (s.EofReached())
                throw new IfaceParseError("Missing function name");
            string funcName = s.ReadString();

            int injectid = dcp.conf.pcodeinjectlib.getPayloadId(InjectPayload.InjectionType.CALLFIXUP_TYPE, fixupName);
            if (injectid < 0)
                throw new IfaceExecutionError("Unknown fixup: " + fixupName);

            string basename;
            Scope funcscope = dcp.conf.symboltab.resolveScopeFromSymbolName(funcName, "::", basename, (Scope)null);
            if (funcscope == (Scope)null)
                throw new IfaceExecutionError("Bad namespace: " + funcName);
            Funcdata fd = funcscope.queryFunction(basename); // Is function already in database
            if (fd == (Funcdata)null)
                throw new IfaceExecutionError("Unknown function name: " + funcName);

            fd.getFuncProto().setInjectId(injectid);
            status.optr.WriteLine("Successfully applied callfixup");
        }
    }
}
