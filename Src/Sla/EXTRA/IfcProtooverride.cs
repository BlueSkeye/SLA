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
    internal class IfcProtooverride : IfaceDecompCommand
    {
        /// \class IfcProtooverride
        /// \brief Override the prototype of a called function: `override prototype <address> <declaration>`
        ///
        /// Force a specified prototype declaration on a called function when decompiling
        /// the current function. The current function must be decompiled again to see the effect.
        /// The called function is indicated by the address of its calling instruction.
        /// The prototype only affects decompilation for the \e current function.
        public override void execute(TextReader s)
        {
            int4 discard;

            if (dcp.fd == (Funcdata*)0)
                throw IfaceExecutionError("No function selected");

            s >> ws;
            Address callpoint(parse_machaddr(s, discard,* dcp.conf.types));
            int4 i;
            for (i = 0; dcp.fd.numCalls(); ++i)
                if (dcp.fd.getCallSpecs(i).getOp().getAddr() == callpoint) break;
            if (i == dcp.fd.numCalls())
                throw IfaceExecutionError("No call is made at this address");

            PrototypePieces pieces;
            parse_protopieces(pieces, s, dcp.conf); // Parse the prototype from stream

            FuncProto* newproto = new FuncProto();

            // Make proto whose storage is internal, not backed by a real scope
            newproto.setInternal(pieces.model, dcp.conf.types.getTypeVoid());
            newproto.setPieces(pieces);
            dcp.fd.getOverride().insertProtoOverride(callpoint, newproto);
            dcp.fd.clear();       // Clear any analysis (this leaves overrides intact)
        }
    }
}
