using System;
using System.Collections.Generic;
using System.Diagnostics.SymbolStore;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.SLACOMP
{
    /// \brief A helper class to associate a \e named Constructor section with its symbol scope
    ///
    /// A Constructor can contain multiple named sections of p-code.  There is a \e main
    /// section associated with the constructor, but other sections are possible and can
    /// be accessed through the \b crossbuild directive, which allows their operations to be
    /// incorporated into nearby instructions. During parsing of a SLEIGH file, \b this class
    /// associates a named section with its dedicated symbol scope.
    internal class RtlPair
    {
        /// A named p-code section
        internal ConstructTpl section;
        /// Symbol scope associated with the section
        internal SymbolScope scope;

        ///< Construct on empty pair
        internal RtlPair()
        {
            section = (ConstructTpl*)0;
            scope = (SymbolScope*)0;
        }

        internal RtlPair(ConstructTpl sec, SymbolScope sc)
        {
            section = sec;
            scope = sc;
        }
    }
}
