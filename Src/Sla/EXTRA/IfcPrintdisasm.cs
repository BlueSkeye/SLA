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
            Architecture* glb;
            Address addr;
            int4 size;
            // TODO add partial listings

            s >> ws;
            if (s.eof())
            {
                if (dcp.fd == (Funcdata*)0)
                    throw IfaceExecutionError("No function selected");
                *status.fileoptr << "Assembly listing for " << dcp.fd.getName() << endl;
                addr = dcp.fd.getAddress();
                size = dcp.fd.getSize();
                glb = dcp.fd.getArch();
            }
            else
            {
                addr = parse_machaddr(s, size, *dcp.conf.types); // Read beginning address
                s >> ws;
                Address offset2 = parse_machaddr(s, size, *dcp.conf.types);
                size = offset2.getOffset() - addr.getOffset();
                glb = dcp.conf;
            }
            IfaceAssemblyEmit assem(status.fileoptr,10);
            while (size > 0)
            {
                int4 sz;
                sz = glb.translate.printAssembly(assem, addr);
                addr = addr + sz;
                size -= sz;
            }
        }
    }
}
