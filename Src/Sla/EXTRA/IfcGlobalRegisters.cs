using Sla.CORE;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

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
            Dictionary<VarnodeData, string> reglist;
            dcp.conf.translate.getAllRegisters(reglist);
            Dictionary<VarnodeData, string>::const_iterator iter;
            AddrSpace* spc = (AddrSpace)null;
            ulong lastoff = 0;
            Scope* globalscope = dcp.conf.symboltab.getGlobalScope();
            int count = 0;
            for (iter = reglist.begin(); iter != reglist.end(); ++iter)
            {
                VarnodeData dat = iter.Current.Key;
                if (dat.space == spc)
                {
                    if (dat.offset <= lastoff) continue; // Nested register def
                }
                spc = dat.space;
                lastoff = dat.offset + dat.size - 1;
                Address addr(spc, dat.offset);
                uint flags = 0;
                // Check if the register location is global
                globalscope.queryProperties(addr, dat.size, new Address(), flags);
                if ((flags & Varnode.varnode_flags.persist) != 0)
                {
                    Datatype* ct = dcp.conf.types.getBase(dat.size, type_metatype.TYPE_UINT);
                    globalscope.addSymbol((*iter).second, ct, addr, new Address());
                    count += 1;
                }
            }
            if (count == 0)
                *status.optr << "No global registers" << endl;
            else
                *status.optr << "Successfully made a global symbol for " << count << " registers" << endl;
        }
    }
}
