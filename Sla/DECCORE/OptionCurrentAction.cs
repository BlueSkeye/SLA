using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ghidra
{
    internal class OptionCurrentAction : ArchOption
    {
        public OptionCurrentAction()
        {
            name = "currentaction";
        }

        /// \class OptionCurrentAction
        /// \brief Toggle a sub-group of actions within a root Action
        ///
        /// If two parameters are given, the first indicates the name of the sub-group, and the second is
        /// the toggle value, "on" or "off". The change is applied to the current root Action.
        ///
        /// If three parameters are given, the first indicates the root Action (which will be set as current)
        /// to modify. The second and third parameters give the name of the sub-group and the toggle value.
        public override string apply(Architecture glb, string p1, string p2, string p3)
        {
            if ((p1.size() == 0) || (p2.size() == 0))
                throw ParseError("Must specify subaction, on/off");
            bool val;
            string res = "Toggled ";

            if (p3.size() != 0)
            {
                glb->allacts.setCurrent(p1);
                val = onOrOff(p3);
                glb->allacts.toggleAction(p1, p2, val);
                res += p2 + " in action " + p1;
            }
            else
            {
                val = onOrOff(p2);
                glb->allacts.toggleAction(glb->allacts.getCurrentName(), p1, val);
                res += p1 + " in action " + glb->allacts.getCurrentName();
            }

            return res;
        }
    }
}
