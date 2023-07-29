using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    internal class OptionSetAction : ArchOption
    {
        public OptionSetAction()
        {
            name = "setaction";
        }

        /// \class OptionSetAction
        /// \brief Establish a new root Action for the decompiler
        ///
        /// The first parameter specifies the name of the root Action. If a second parameter
        /// is given, it specifies the name of a new root Action, which  is created by copying the
        /// Action specified with the first parameter.  In this case, the current root Action is
        /// set to the new copy, which can then by modified
        public override string apply(Architecture glb, string p1, string p2, string p3)
        {
            if (p1.size() == 0)
                throw ParseError("Must specify preexisting action");

            if (p2.size() != 0)
            {
                glb.allacts.cloneGroup(p1, p2);
                glb.allacts.setCurrent(p2);
                return "Created " + p2 + " by cloning " + p1 + " and made it current";
            }
            glb.allacts.setCurrent(p1);
            return "Set current action to " + p1;
        }
    }
}
