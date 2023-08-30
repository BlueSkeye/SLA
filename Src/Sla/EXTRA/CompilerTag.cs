using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sla.CORE;

namespace Sla.EXTRA
{
    /// \brief Contents of a \<compiler> tag in a .ldefs file
    ///
    /// This class describes a compiler specification file as referenced by the Sleigh language subsystem.
    internal class CompilerTag
    {
        private string name;          ///< (Human readable) name of the compiler
        private string spec;          ///< cspec file for this compiler
        private string id;            ///< Unique id for this compiler

        public CompilerTag()
        {
        }

        /// Restore the record from an XML stream
        /// Parse file attributes from a \<compiler> element
        /// \param decoder is the stream decoder
        public void decode(Sla.CORE.Decoder decoder)
        {
            ElementId elemId = decoder.openElement(ElementId.ELEM_COMPILER);
            name = decoder.readString(AttributeId.ATTRIB_NAME);
            spec = decoder.readString(AttributeId.ATTRIB_SPEC);
            id = decoder.readString(AttributeId.ATTRIB_ID);
            decoder.closeElement(elemId);
        }

        /// Get the human readable name of the spec
        public string getName() => name;

        /// Get the file-name
        public string getSpec() => spec;

        /// Get the string used as part of \e language \e id
        public string getId() => id;
    }
}
