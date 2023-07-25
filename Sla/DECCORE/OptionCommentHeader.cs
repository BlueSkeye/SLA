using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    internal class OptionCommentHeader : ArchOption
    {
        public OptionCommentHeader()
        {
            name = "commentheader";
        }

        /// \class OptionCommentHeader
        /// \brief Toggle whether different comment \e types are emitted by the decompiler in the header for a function
        ///
        /// The first parameter specifies the comment type: "header" and "warningheader"
        /// The second parameter is the toggle value "on" or "off".
        public override string apply(Architecture glb, string p1, string p2, string p3)
        {
            bool toggle = onOrOff(p2);
            uint4 flags = glb->print->getHeaderComment();
            uint4 val = Comment::encodeCommentType(p1);
            if (toggle)
                flags |= val;
            else
                flags &= ~val;
            glb->print->setHeaderComment(flags);
            string prop;
            prop = toggle ? "on" : "off";
            return "Header comment type " + p1 + " turned " + prop;
        }
    }
}
