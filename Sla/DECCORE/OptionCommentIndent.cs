using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ghidra
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
            istringstream s(p1);
            s.unsetf(ios::dec | ios::hex | ios::oct);
            int4 val = -1;
            s >> val;
            if (val == -1)
                throw ParseError("Must specify integer comment indent");
            glb->print->setLineCommentIndent(val);
            return "Comment indent set to " + p1;
        }
    }
}
