using Sla.EXTRA;

namespace Sla.DECCORE
{
    internal class OptionStructAlign : ArchOption
    {
        public OptionStructAlign()
        {
            name = "structalign";
        }

        /// \class OptionStructAlign
        /// \brief Alter the "structure alignment" data organization setting
        ///
        /// The first parameter must an integer value indicating the desired alignment
        public override string apply(Architecture glb, string p1, string p2, string p3)
        {
            int val = -1;
            TextReader s = new StringReader(p1);
            if (!int.TryParse(s.ReadString(), out val))
                throw new ParseError("Missing alignment value");
            glb.types.setStructAlign(val);
            return "Structure alignment set";
        }
    }
}
