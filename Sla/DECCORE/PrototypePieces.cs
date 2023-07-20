using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Text;
using System.Threading.Tasks;

namespace ghidra
{
    /// \brief Raw components of a function prototype (obtained from parsing source code)
    internal class PrototypePieces
    {
        /// (Optional) model on which prototype is based
        internal ProtoModel model;
        /// Identifier (function name) associated with prototype
        internal string name;
        /// Return data-type
        internal Datatype outtype;
        /// Input data-types
        internal List<Datatype> intypes;
        /// Identifiers for input types
        internal List<string> innames;
        /// True if prototype takes variable arguments
        internal bool dotdotdot;
    }
}
