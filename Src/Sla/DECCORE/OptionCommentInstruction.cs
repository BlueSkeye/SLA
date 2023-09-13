using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    internal class OptionCommentInstruction : ArchOption
    {
        public OptionCommentInstruction()
        {
            name = "commentinstruction";
        }

        /// \class OptionCommentInstruction
        /// \brief Toggle whether different comment \e types are emitted by the decompiler in the body of a function
        ///
        /// The first parameter specifies the comment type: "warning", "user1", "user2", etc.
        /// The second parameter is the toggle value "on" or "off".
        public override string apply(Architecture glb, string p1, string p2, string p3)
        {
            bool toggle = onOrOff(p2);
            Comment.comment_type flags = glb.print.getInstructionComment();
            Comment.comment_type val = Comment.encodeCommentType(p1);
            if (toggle)
                flags |= val;
            else
                flags &= ~val;
            glb.print.setInstructionComment(flags);
            string prop;
            prop = toggle ? "on" : "off";
            return "Instruction comment type " + p1 + " turned " + prop;
        }
    }
}
