﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    /// \brief The lowest level error generated by the decompiler
    ///
    /// This is the base error for all exceptions thrown by the
    /// decompiler.  This underived form is thrown for very low
    /// level errors that immediately abort decompilation (usually
    /// for just a single function).
    internal class LowlevelError : Exception
    {
        /// Explanatory string
        private string explain;
        
        /// Initialize the error with an explanatory string
        internal LowlevelError(string s)
            :base(s)
        {
            explain = s;
        }
    }
}
