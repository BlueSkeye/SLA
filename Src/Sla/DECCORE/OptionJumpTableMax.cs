using Sla.EXTRA;

namespace Sla.DECCORE
{
    internal class OptionJumpTableMax : ArchOption
    {
        public OptionJumpTableMax()
        {
            name = "jumptablemax";
        }

        /// \class OptionJumpTableMax
        /// \brief Set the maximum number of entries that can be recovered for a single jump table
        ///
        /// This option is an unsigned integer value used during analysis of jump tables.  It serves as a
        /// sanity check that the recovered number of entries for a jump table is reasonable and
        /// also acts as a resource limit on the number of destination addresses that analysis will attempt
        /// to follow from a single indirect jump.
        public override string apply(Architecture glb, string p1, string p2, string p3)
        {
            TextReader s = new StringReader(p1);
            /// s.unsetf(ios::dec | ios::hex | ios::oct);
            uint val = 0;
            if (!uint.TryParse(s.ReadString(), out val))
                throw new ParseError("Must specify integer maximum");
            glb.max_jumptable_size = val;
            return "Maximum jumptable size set to " + p1;
        }
    }
}
