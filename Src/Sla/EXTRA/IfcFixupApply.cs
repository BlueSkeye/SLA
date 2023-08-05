using Sla.DECCORE;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

            string fixupName, funcName;

            s >> ws;
            if (s.eof())
                throw new IfaceParseError("Missing fixup name");
            s >> fixupName >> ws;
            if (s.eof())
                throw new IfaceParseError("Missing function name");
            s >> funcName;

            int injectid = dcp.conf.pcodeinjectlib.getPayloadId(InjectPayload.InjectionType.CALLFIXUP_TYPE, fixupName);
            if (injectid < 0)
                throw new IfaceExecutionError("Unknown fixup: " + fixupName);

            string basename;
            Scope* funcscope = dcp.conf.symboltab.resolveScopeFromSymbolName(funcName, "::", basename, (Scope)null);
            if (funcscope == (Scope)null)
                throw new IfaceExecutionError("Bad namespace: " + funcName);
            Funcdata* fd = funcscope.queryFunction(basename); // Is function already in database
            if (fd == (Funcdata)null)
                throw new IfaceExecutionError("Unknown function name: " + funcName);

            fd.getFuncProto().setInjectId(injectid);
            *status.optr << "Successfully applied callfixup" << endl;
        }
    }
}
