using Sla.CORE;
using Sla.DECCORE;

namespace Sla.EXTRA
{
    internal class IfcGlobalRegisters : IfaceDecompCommand
    {
        /// \class IfcGlobalRegisters
        /// \brief Name global registers: `global registers`
        ///
        /// Name any global symbol stored in a register with the name of the register.
        public override void execute(TextReader s)
        {
            if (dcp.conf == (Architecture)null)
                throw new IfaceExecutionError("No load image present");
            Dictionary<VarnodeData, string> reglist = new Dictionary<VarnodeData, string>();
            dcp.conf.translate.getAllRegisters(reglist);
            AddrSpace? spc = (AddrSpace)null;
            ulong lastoff = 0;
            Scope globalscope = dcp.conf.symboltab.getGlobalScope();
            int count = 0;
            foreach (KeyValuePair< VarnodeData, string> pair in reglist) {
                VarnodeData dat = pair.Key;
                if (dat.space == spc) {
                    if (dat.offset <= lastoff) continue; // Nested register def
                }
                spc = dat.space;
                lastoff = dat.offset + dat.size - 1;
                Address addr = new Address(spc, dat.offset);
                Varnode.varnode_flags flags = 0;
                // Check if the register location is global
                globalscope.queryProperties(addr, (int)dat.size, new Address(), flags);
                if ((flags & Varnode.varnode_flags.persist) != 0) {
                    Datatype ct = dcp.conf.types.getBase((int)dat.size, type_metatype.TYPE_UINT);
                    globalscope.addSymbol(pair.Value, ct, addr, new Address());
                    count += 1;
                }
            }
            if (count == 0)
                status.optr.WriteLine("No global registers");
            else
                status.optr.WriteLine($"Successfully made a global symbol for {count} registers");
        }
    }
}
