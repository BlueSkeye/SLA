using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    internal class OptionStructAlign : ArchOption
    {
        public OptionStructAlign()
        {
            name = "structalign";
        }

        /// \class OptionStructAlign
        /// \brief Alter the "structure alignment" data organization setting
        ///
        /// The first parameter must an integer value indicating the desired alignment
        public override string apply(Architecture glb, string p1, string p2, string p3)
        {
            int val = -1;
            istringstream s(p1);
            s >> dec >> val;
            if (val == -1)
                throw ParseError("Missing alignment value");

            glb.types.setStructAlign(val);
            return "Structure alignment set";
        }
    }
}
