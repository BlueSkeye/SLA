using Sla.EXTRA;
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
            if (p1.Length == 0)
                throw new ParseError("Must specify number of instructions");

            int newMax = -1;
            // Let user specify base
            newMax = int.Parse(p1);
            if (newMax < 0)
                throw new ParseError("Bad maxinstruction parameter");
            glb.max_instructions = (uint)newMax;
            return "Maximum instructions per function set";
        }
    }
}
