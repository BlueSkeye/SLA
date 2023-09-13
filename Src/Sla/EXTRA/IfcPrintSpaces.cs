using Sla.CORE;
using Sla.DECCORE;

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
            if (dcp.conf == (Architecture)null)
                throw new IfaceExecutionError("No load image present");

            AddrSpaceManager manage = dcp.conf;
            int num = manage.numSpaces();
            for (int i = 0; i < num; ++i) {
                AddrSpace spc = manage.getSpace(i);
                if (spc == (AddrSpace)null) continue;
                status.fileoptr.Write($"{spc.getIndex()} : '{spc.getShortcut()}' {spc.getName()}");
                if (spc.getType() == spacetype.IPTR_CONSTANT)
                    status.fileoptr.Write(" constant ");
                else if (spc.getType() == spacetype.IPTR_PROCESSOR)
                    status.fileoptr.Write(" processor");
                else if (spc.getType() == spacetype.IPTR_SPACEBASE)
                    status.fileoptr.Write(" spacebase");
                else if (spc.getType() == spacetype.IPTR_INTERNAL)
                    status.fileoptr.Write(" internal ");
                else
                    status.fileoptr.Write(" special  ");
                if (spc.isBigEndian())
                    status.fileoptr.Write(" big  ");
                else
                    status.fileoptr.Write(" small");
                status.fileoptr.Write($" addrsize={spc.getAddrSize()} wordsize={spc.getWordSize()}");
                status.fileoptr.Write($" delay={spc.getDelay()}");
                status.fileoptr.WriteLine();
            }
        }
    }
}
