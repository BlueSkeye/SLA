﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief An input or output parameter to a p-code injection payload
    ///
    /// Within the chunk of p-code being injected, this is a placeholder for Varnodes
    /// that serve as inputs or outputs to the chunk, which are filled-in in the context
    /// of the injection.  For instance, for a \e call-fixup that injects a user-defined
    /// p-code op, the input Varnodes would be substituted with the actual input Varnodes
    /// to the user-defined op.
    internal class InjectParameter
    {
        // friend class InjectPayload;
        /// Name of the parameter (for use in parsing p-code \e source)
        private string name;
        /// Unique index assigned (for cross referencing associated Varnode in the InjectContext)
        internal int index;
        /// Size of the parameter Varnode in bytes
        private uint size;
        
        public InjectParameter(string nm, uint sz)
        {
            name = nm;
            index = 0;
            size = sz;
        }

        /// Get the parameter name
        public string getName() => name;

        /// Get the assigned index
        public int getIndex() => index;

        /// Get the size of the parameter in bytes
        public uint getSize() => size;
    }
}
