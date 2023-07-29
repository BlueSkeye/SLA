using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.CORE
{
    /// \brief A class for writing structured data to a stream
    /// The resulting encoded data is structured similarly to an XML document. The document contains a nested set
    /// of \b elements, with labels corresponding to the ElementId class. A single element can hold
    /// zero or more attributes and zero or more child elements.  An \b attribute holds a primitive
    /// data element (bool, integer, string) and is labeled by an AttributeId. The document is written
    /// using a sequence of openElement() and closeElement() calls, intermixed with write*() calls to encode
    /// the data primitives.  All primitives written using a write*() call are associated with current open element,
    /// and all write*() calls for one element must come before opening any child element.
    /// The traditional XML element text content can be written using the special ATTRIB_CONTENT AttributeId, which
    /// must be the last write*() call associated with the specific element.
    public abstract class Encoder
    {
        ///< Destructor
        ~Encoder()
        {
        }

        /// \brief Begin a new element in the encoding
        /// The element will have the given ElementId annotation and becomes the \e current element.
        /// \param elemId is the given ElementId annotation
        public abstract void openElement(ElementId elemId);

        /// \brief End the current element in the encoding
        /// The current element must match the given annotation or an exception is thrown.
        /// \param elemId is the given (expected) annotation for the current element
        public abstract void closeElement(ElementId elemId);

        /// \brief Write an annotated boolean value into the encoding
        /// The boolean data is associated with the given AttributeId annotation and the current open element.
        /// \param attribId is the given AttributeId annotation
        /// \param val is boolean value to encode
        public abstract void writeBool(AttributeId attribId, bool val);

        /// \brief Write an annotated signed integer value into the encoding
        /// The integer is associated with the given AttributeId annotation and the current open element.
        /// \param attribId is the given AttributeId annotation
        /// \param val is the signed integer value to encode
        public abstract void writeSignedInteger(AttributeId attribId, long val);

        /// \brief Write an annotated unsigned integer value into the encoding
        /// The integer is associated with the given AttributeId annotation and the current open element.
        /// \param attribId is the given AttributeId annotation
        /// \param val is the unsigned integer value to encode
        public abstract void writeUnsignedInteger(AttributeId attribId, ulong val);

        /// \brief Write an annotated string into the encoding
        /// The string is associated with the given AttributeId annotation and the current open element.
        /// \param attribId is the given AttributeId annotation
        /// \param val is the string to encode
        public abstract void writeString(AttributeId attribId, string val);

        /// \brief Write an annotated string, using an indexed attribute, into the encoding
        /// Multiple attributes with a shared name can be written to the same element by calling this method
        /// multiple times with a different \b index value. The encoding will use attribute ids up to the base id
        /// plus the maximum index passed @in.  Implementors must be careful to not use other attributes with ids
        /// bigger than the base id within the element taking the indexed attribute.
        /// \param attribId is the shared AttributeId
        /// \param index is the unique index to associated with the string
        /// \param val is the string to encode
        public abstract void writeStringIndexed(AttributeId attribId, uint index, string val);

        /// \brief Write an address space reference into the encoding
        /// The address space is associated with the given AttributeId annotation and the current open element.
        /// \param attribId is the given AttributeId annotation
        /// \param spc is the address space to encode
        public abstract void writeSpace(AttributeId attribId, AddrSpace spc);
    }
}
