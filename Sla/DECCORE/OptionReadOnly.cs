using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ghidra
{
    internal class OptionReadOnly : ArchOption
    {
        public OptionReadOnly()
        {
            name = "readonly";
        }

        /// \class OptionReadOnly
        /// \brief Toggle whether read-only memory locations have their value propagated
        ///
        /// Setting this to "on", causes the decompiler to treat read-only memory locations as
        /// constants that can be propagated.
        public override string apply(Architecture glb, string p1, string p2, string p3)
        {
            if (p1.size() == 0)
                throw ParseError("Read-only option must be set \"on\" or \"off\"");
            glb->readonlypropagate = onOrOff(p1);
            if (glb->readonlypropagate)
                return "Read-only memory locations now propagate as constants";
            return "Read-only memory locations now do not propagate";
        }
    }
}
