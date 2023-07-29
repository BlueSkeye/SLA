using Sla.CORE;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    internal class IfcPrintSpaces : IfaceDecompCommand
    {
        /// \class IfcPrintSpaces
        /// \brief Print all address spaces: `print spaces`
        ///
        /// Information about every address space in the architecture/program is written
        /// to the console.
        public override void execute(TextReader s)
        {
            if (dcp.conf == (Architecture*)0)
                throw IfaceExecutionError("No load image present");

            AddrSpaceManager manage = dcp.conf;
            int num = manage.numSpaces();
            for (int i = 0; i < num; ++i)
            {
                AddrSpace* spc = manage.getSpace(i);
                if (spc == (AddrSpace*)0) continue;
                *status.fileoptr << dec << spc.getIndex() << " : '" << spc.getShortcut() << "' " << spc.getName();
                if (spc.getType() == IPTR_CONSTANT)
                    *status.fileoptr << " constant ";
                else if (spc.getType() == IPTR_PROCESSOR)
                    *status.fileoptr << " processor";
                else if (spc.getType() == IPTR_SPACEBASE)
                    *status.fileoptr << " spacebase";
                else if (spc.getType() == IPTR_INTERNAL)
                    *status.fileoptr << " internal ";
                else
                    *status.fileoptr << " special  ";
                if (spc.isBigEndian())
                    *status.fileoptr << " big  ";
                else
                    *status.fileoptr << " small";
                *status.fileoptr << " addrsize=" << spc.getAddrSize() << " wordsize=" << spc.getWordSize();
                *status.fileoptr << " delay=" << spc.getDelay();
                *status.fileoptr << endl;
            }
        }
    }
}
