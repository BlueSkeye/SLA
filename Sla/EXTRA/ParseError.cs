﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    /// \brief An error generated while parsing a command or language
    ///
    /// This error is generated when parsing character data of some
    /// form, as in a user command from the console or when parsing
    /// C syntax.
    internal class ParseError : LowlevelError
    {
        // Parsing error
        /// Initialize the error with an explanatory string
        internal ParseError(string s)
            : base(s)
        {
        }
    }
}
