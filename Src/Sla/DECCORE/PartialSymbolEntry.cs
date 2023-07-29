using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief A structure for pushing nested fields to the RPN stack
    ///
    /// A helper class for unraveling a nested reference to a field. It links the
    /// data-type, field name, field object, and token together
    internal struct PartialSymbolEntry
    {
        /// Operator used to drill-down to the field
        internal OpToken token;
        /// The component object describing the field
        internal TypeField field;
        /// The parent data-type owning the field
        internal Datatype parent;
        /// The name of the field
        internal string fieldname;
        /// Highlight information for the field token
        internal EmitMarkup::syntax_highlight hilite;
    }
}
