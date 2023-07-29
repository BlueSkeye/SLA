using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    internal class OptionMaxInstruction : ArchOption
    {
        public OptionMaxInstruction()
        {
            name = "maxinstruction";
        }

        /// \class OptionMaxInstruction
        /// \brief Maximum number of instructions that can be processed in a single function
        ///
        /// The first parameter is an integer specifying the maximum.
        public override string apply(Architecture glb, string p1, string p2, string p3)
        {
            if (p1.size() == 0)
                throw ParseError("Must specify number of instructions");

            int4 newMax = -1;
            istringstream s1(p1);
            s1.unsetf(ios::dec | ios::hex | ios::oct); // Let user specify base
            s1 >> newMax;
            if (newMax < 0)
                throw ParseError("Bad maxinstruction parameter");
            glb.max_instructions = newMax;
            return "Maximum instructions per function set";
        }
    }
}
