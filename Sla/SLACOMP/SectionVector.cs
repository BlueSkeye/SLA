using Sla.SLACOMP;
using System;
using System.Collections.Generic;
using System.Diagnostics.SymbolStore;
using System.Linq;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Text;
using System.Threading.Tasks;

namespace Sla.SLACOMP
{
    /// \brief A collection of \e named p-code sections for a single Constructor
    ///
    /// A Constructor always has a \b main section of p-code (which may be empty).
    /// Alternately a Constructor may define additional \e named sections of p-code.
    /// Operations in these sections are emitted using the \b crossbuild directive and
    /// can be incorporated into following instructions.
    ///
    /// Internally different sections (RtlPair) are identified by index.  A
    /// SectionSymbol holds the section's name and its corresponding index.
    internal class SectionVector
    {
        /// Index of the section currently being parsed.
        private int4 nextindex;
        /// The main section
        private RtlPair main;
        /// Named sections accessed by index
        private List<RtlPair> named;

        /// This must be constructed with the \e main section of p-code, which can contain no p-code
        /// \param rtl is the \e main section of p-code
        /// \param scope is the symbol scope associated with the section
        public SectionVector(ConstructTpl rtl, SymbolScope scope)
        {
            nextindex = -1;
            main.section = rtl;
            main.scope = scope;
        }

        /// Get the \e main section
        public ConstructTpl getMainSection() => main.section;

        /// Get a \e named section by index
        public ConstructTpl getNamedSection(int4 index) => named[index].section;

        /// Get the \e main section/namespace pair
        public RtlPair getMainPair() => main;

        /// Get a \e named section/namespace pair by index
        public RtlPair getNamedPair(int4 i) => named[i];

        /// Set the index of the currently parsing \e named section
        public void setNextIndex(int4 i)
        {
            nextindex = i;
        }

        /// Get the maximum (exclusive) named section index
        public int4 getMaxId() => named.size();

        /// Add a new \e named section
        /// Associate the new section with \b nextindex, established prior to parsing
        /// \param rtl is the \e named section of p-code
        /// \param scope is the associated symbol scope
        public void append(ConstructTpl rtl, SymbolScope scope)
        {
            while (named.size() <= nextindex)
                named.emplace_back();
            named[nextindex] = RtlPair(rtl, scope);
        }
    }
}
