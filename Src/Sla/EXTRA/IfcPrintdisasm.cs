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
    internal class IfcPrintdisasm : IfaceDecompCommand
    {
        /// \class IfcPrintdisasm
        /// \brief Print disassembly of a memory range: `disassemble [<address1> <address2>]`
        ///
        /// If no addresses are provided, disassembly for the current function is displayed.
        /// Otherwise disassembly is between the two provided addresses.
        public override void execute(TextReader s)
        {
            Architecture glb;
            Address addr;
            int size;
            // TODO add partial listings

            s.ReadSpaces();
            if (s.EofReached()) {
                if (dcp.fd == (Funcdata)null)
                    throw new IfaceExecutionError("No function selected");
                status.fileoptr.WriteLine("Assembly listing for {dcp.fd.getName()}");
                addr = dcp.fd.getAddress();
                size = dcp.fd.getSize();
                glb = dcp.fd.getArch();
            }
            else {
                addr = Grammar.parse_machaddr(s, out size, dcp.conf.types); // Read beginning address
                s.ReadSpaces();
                Address offset2 = Grammar.parse_machaddr(s, out size, dcp.conf.types);
                size = (int)(offset2.getOffset() - addr.getOffset());
                glb = dcp.conf;
            }
            IfaceAssemblyEmit assem = new IfaceAssemblyEmit(status.fileoptr,10);
            while (size > 0) {
                int sz;
                sz = glb.translate.printAssembly(assem, addr);
                addr = addr + sz;
                size -= sz;
            }
        }
    }
}
