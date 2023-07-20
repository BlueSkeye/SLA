using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ghidra
{
    internal class OptionIntegerFormat : ArchOption
    {
        public OptionIntegerFormat()
        {
            name = "integerformat";
        }

        /// \class OptionIntegerFormat
        /// \brief Set the formatting strategy used by the decompiler to emit integers
        ///
        /// The first parameter is the strategy name: "hex", "dec", or "best"
        public override string apply(Architecture glb, string p1, string p2, string p3)
        {
            glb->print->setIntegerFormat(p1);
            return "Integer format set to " + p1;
        }
    }
}
