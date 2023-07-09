using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ghidra
{
    /// \brief A complete in-memory XML document.
    /// This is actually just an Element object itself, with the document's \e root element
    /// as its only child, which owns all the child documents below it in DOM the hierarchy.
    public class Document : Element
    {
        /// Construct an (empty) document
        public Document()
            : base(null)
        {
        }

        public void Dispose()
        {
            Dispose(true);
        }

        protected void Dispose(bool disposing)
        {
            if (disposing) {
                GC.SuppressFinalize(this);
            }
        }

        /// Get the root Element of the document
        public Element? getRoot()
        {
            return (null == children) ? null : children[0];
        }
    }
}
