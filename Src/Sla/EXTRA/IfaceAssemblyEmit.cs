using Sla.CORE;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    /// \brief Disassembly emitter that prints to a console stream
    ///
    /// An instruction is printed to a stream simply, as an address
    /// followed by the mnemonic and then column aligned operands.
    internal class IfaceAssemblyEmit : AssemblyEmit
    {
        private int4 mnemonicpad;       ///< How much to pad the mnemonic
        private TextWriter s;         ///< The current stream to write to

        public IfaceAssemblyEmit(TextWriter val, int4 mp)
        {
            s = val;
            mnemonicpad = mp;
        }

        public override void dump(Address addr, string mnem, string body)
        {
            addr.printRaw(*s);
            *s << ": " << mnem;
            for (int4 i = mnem.size(); i < mnemonicpad; ++i) *s << ' ';
            *s << body << endl;
        }
    }
}
