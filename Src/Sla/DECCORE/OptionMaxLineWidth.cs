using Sla.EXTRA;

namespace Sla.DECCORE
{
    internal class OptionMaxLineWidth : ArchOption
    {
        public OptionMaxLineWidth()
        {
            name = "maxlinewidth";
        }

        /// \class OptionMaxLineWidth
        /// \brief Set the maximum number of characters per decompiled line
        ///
        /// The first parameter is an integer value passed to the pretty printer as the maximum
        /// number of characters to emit in a single line before wrapping.
        public override string apply(Architecture glb, string p1, string p2, string p3)
        {
            TextReader s = new StringReader(p1);
            // s.unsetf(ios::dec | ios::hex | ios::oct);
            int val = -1;
            if (!int.TryParse(s.ReadString(), out val))
                throw new ParseError("Must specify integer linewidth");
            glb.print.setMaxLineSize(val);
            return "Maximum line width set to " + p1;
        }
    }
}
