﻿using Sla.DECCORE;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    internal class IfcAdjustVma : IfaceDecompCommand
    {
        /// \class IfcAdjustVma
        /// \brief Change the base address of the load image: `adjust vma 0xabcd0123`
        ///
        /// The provided parameter is added to the current base address of the image.
        /// This only affects the address of bytes in the image and so should be done
        /// before functions and other symbols are layed down.
        public override void execute(TextReader s)
        {
            ulong adjust = 0uL;
            if (dcp.conf == (Architecture)null)
                throw new IfaceExecutionError("No load image present");
            // s.unsetf(ios::dec | ios::hex | ios::oct); // Let user specify base
            s.ReadSpaces();
            if (!ulong.TryParse(s.ReadString(), out adjust))
                throw new IfaceParseError("No adjustment parameter");
            dcp.conf.loader.adjustVma(adjust);
        }
    }
}
