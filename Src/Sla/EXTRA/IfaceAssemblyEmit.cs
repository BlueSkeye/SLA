using Sla.CORE;

namespace Sla.EXTRA
{
    /// \brief Disassembly emitter that prints to a console stream
    ///
    /// An instruction is printed to a stream simply, as an address
    /// followed by the mnemonic and then column aligned operands.
    internal class IfaceAssemblyEmit : AssemblyEmit
    {
        private int mnemonicpad;       ///< How much to pad the mnemonic
        private TextWriter s;         ///< The current stream to write to

        public IfaceAssemblyEmit(TextWriter val, int mp)
        {
            s = val;
            mnemonicpad = mp;
        }

        public override void dump(Address addr, string mnem, string body)
        {
            addr.printRaw(s);
            s.Write($": {mnem}");
            for (int i = mnem.Length; i < mnemonicpad; ++i) s.Write(' ');
            s.WriteLine(body);
        }
    }
}
