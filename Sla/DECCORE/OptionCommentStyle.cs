using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ghidra
{
    internal class OptionCommentStyle : ArchOption
    {
        public OptionCommentStyle()
        {
            name = "commentstyle";
        }

        /// \class OptionCommentStyle
        /// \brief Set the style of comment emitted by the decompiler
        ///
        /// The first parameter is either "c", "cplusplus", a string starting with "/*", or a string starting with "//"
        public override string apply(Architecture glb, string p1, string p2, string p3)
        {
            glb->print->setCommentStyle(p1);
            return "Comment style set to " + p1;
        }
    }
}
