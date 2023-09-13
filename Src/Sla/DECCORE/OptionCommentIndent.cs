using Sla.EXTRA;

namespace Sla.DECCORE
{
    internal class OptionCommentIndent : ArchOption
    {
        public OptionCommentIndent()
        {
            name = "commentindent";
        }

        /// \class OptionCommentIndent
        /// \brief How many characters to indent comment lines.
        ///
        /// The first parameter gives the integer value.  Comment lines are indented this much independent
        /// of the associated code's nesting depth.
        public override string apply(Architecture glb, string p1, string p2, string p3)
        {
            TextReader s = new StringReader(p1);
            // s.unsetf(ios::dec | ios::hex | ios::oct);
            int val = -1;
            if (!int.TryParse(s.ReadString(), out val)) {
                throw new ParseError("Must specify integer comment indent");
            }
            glb.print.setLineCommentIndent(val);
            return "Comment indent set to " + p1;
        }
    }
}
