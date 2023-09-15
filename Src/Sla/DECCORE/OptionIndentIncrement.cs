using Sla.EXTRA;

namespace Sla.DECCORE
{
    internal class OptionIndentIncrement : ArchOption
    {
        public OptionIndentIncrement()
        {
            name = "indentincrement";
        }

        /// \class OptionIndentIncrement
        /// \brief Set the number of characters to indent per nested scope.
        ///
        /// The first parameter is the integer value specifying how many characters to indent.
        public override string apply(Architecture glb, string p1, string p2, string p3)
        {
            TextReader s = new StringReader(p1);
            // s.unsetf(ios::dec | ios::hex | ios::oct);
            int val = -1;
            if (!int.TryParse(s.ReadString(), out val))
                throw new ParseError("Must specify integer increment");
            glb.print.setIndentIncrement(val);
            return "Characters per indent level set to " + p1;
        }
    }
}
