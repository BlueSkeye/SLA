using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.CORE
{
    /// \brief A class for reading structured data from a stream
    /// All data is loosely structured as with an XML document.  A document contains a nested set
    /// of \b elements, with labels corresponding to the ElementId class. A single element can hold
    /// zero or more attributes and zero or more child elements.  An attribute holds a primitive
    /// data element (bool, integer, string) and is labeled by an AttributeId. The document is traversed
    /// using a sequence of openElement() and closeElement() calls, intermixed with read*() calls to extract
    /// the data. The elements are traversed in a depth first order.  Attributes within an element can
    /// be traversed in order using repeated calls to the getNextAttributeId() method, followed by a calls to
    /// one of the read*(void) methods to extract the data.  Alternately a read*(AttributeId) call can be used
    /// to extract data for an attribute known to be in the element.  There is a special content attribute
    /// whose data can be extracted using a read*(AttributeId) call that is passed the special ATTRIB_CONTENT id.
    /// This attribute will not be traversed by getNextAttribute().
    public abstract class Decoder
    {
        ///< Manager for decoding address space attributes
        protected readonly AddrSpaceManager spcManager;

        /// Base constructor
        public Decoder(AddrSpaceManager spc)
        {
            spcManager = spc;
        }

        /// Get the manager used for address space decoding
        public AddrSpaceManager getAddrSpaceManager() => spcManager;

        ///< Destructor
        ~Decoder()
        {
        }

        /// \brief Prepare to decode a given stream
        /// Called once before any decoding.  Currently this is assumed to make an internal copy of the stream data,
        /// i.e. the input stream is cleared before any decoding takes place.
        /// \param s is the given input stream to be decode
        /// \return \b true if the stream was fully ingested
        public abstract void ingestStream(StreamReader s);

        /// \brief Peek at the next child element of the current parent, without traversing in (opening) it.
        /// The element id is returned, which can be compared to ElementId labels.
        /// If there are no remaining child elements to traverse, 0 is returned.
        /// \return the element id or 0
        public abstract uint peekElement();

        /// \brief Open (traverse into) the next child element of the current parent.
        /// The child becomes the current parent.  The list of attributes is initialized for use with getNextAttributeId.
        /// \return the id of the child element
        public abstract ElementId openElement();

        /// \brief Open (traverse into) the next child element, which must be of a specific type
        /// The child becomes the current parent, and its attributes are initialized for use with getNextAttributeId.
        /// The child must match the given element id or an exception is thrown.
        /// \param elemId is the given element id to match
        /// \return the id of the child element
        public abstract ElementId openElement(ElementId elemId);

        /// \brief Close the current element
        /// The data for the current element is considered fully processed.  If the element has additional children,
        /// an exception is thrown.  The stream must indicate the end of the element in some way.
        /// \param id is the id of the element to close (which must be the current element)
        public abstract void closeElement(ElementId id);

        /// \brief Close the current element, skipping any child elements that have not yet been parsed
        /// This closes the given element, which must be current.  If there are child elements that have not been
        /// parsed, this is not considered an error, and they are skipped over in the parse.
        /// \param id is the id of the element to close (which must be the current element)
        public abstract void closeElementSkipping(uint id);

        /// \brief Get the next attribute id for the current element
        /// Attributes are automatically set up for traversal using this method, when the element is opened.
        /// If all attributes have been traversed (or there are no attributes), 0 is returned.
        /// \return the id of the next attribute or 0
        public abstract AttributeId getNextAttributeId();

        /// \brief Get the id for the (current) attribute, assuming it is indexed
        /// Assuming the previous call to getNextAttributeId() returned the id of ATTRIB_UNKNOWN,
        /// reinterpret the attribute as being an indexed form of the given attribute. If the attribute
        /// matches, return this indexed id, otherwise return ATTRIB_UNKNOWN.
        /// \param attribId is the attribute being indexed
        /// \return the indexed id or ATTRIB_UNKNOWN
        public abstract uint getIndexedAttributeId(AttributeId attribId);

        /// \brief Reset attribute traversal for the current element
        /// Attributes for a single element can be traversed more than once using the getNextAttributeId method.
        public abstract void rewindAttributes();

        /// \brief Parse the current attribute as a boolean value
        /// The last attribute, as returned by getNextAttributeId, is treated as a boolean, and its value is returned.
        /// \return the boolean value associated with the current attribute.
        public abstract bool readBool();

        /// \brief Find and parse a specific attribute in the current element as a boolean value
        /// The set of attributes for the current element is searched for a match to the given attribute id.
        /// This attribute is then parsed as a boolean and its value returned.
        /// If there is no attribute matching the id, an exception is thrown.
        /// Parsing via getNextAttributeId is reset.
        /// \param attribId is the specific attribute id to match
        /// \return the boolean value
        public abstract bool readBool(AttributeId attribId);

        /// \brief Parse the current attribute as a signed integer value
        /// The last attribute, as returned by getNextAttributeId, is treated as a signed integer, and its value is returned.
        /// \return the signed integer value associated with the current attribute.
        public abstract long readSignedInteger();

        /// \brief Find and parse a specific attribute in the current element as a signed integer
        /// The set of attributes for the current element is searched for a match to the given attribute id.
        /// This attribute is then parsed as a signed integer and its value returned.
        /// If there is no attribute matching the id, an exception is thrown.
        /// Parsing via getNextAttributeId is reset.
        /// \param attribId is the specific attribute id to match
        /// \return the signed integer value
        public abstract long readSignedInteger(AttributeId attribId);

        /// \brief Parse the current attribute as either a signed integer value or a string.
        /// If the attribute is an integer, its value is returned. If the attribute is a string, it must match an
        /// expected string passed to the method, and a predetermined integer value associated with the string is returned.
        /// If the attribute neither matches the expected string nor is an integer, the return value is undefined.
        /// \param expect is the string value to expect if the attribute is encoded as a string
        /// \param expectval is the integer value to return if the attribute matches the expected string
        /// \return the encoded integer or the integer value associated with the expected string
        public abstract long readSignedIntegerExpectString(string expect, long expectval);

        /// \brief Find and parse a specific attribute in the current element as either a signed integer or a string.
        /// If the attribute is an integer, its value is parsed and returned.
        /// If the attribute is encoded as a string, it must match an expected string passed to this method.
        /// In this case, a predetermined integer value is passed back, indicating a matching string was parsed.
        /// If the attribute neither matches the expected string nor is an integer, the return value is undefined.
        /// If there is no attribute matching the id, an exception is thrown.
        /// \param attribId is the specific attribute id to match
        /// \param expect is the string to expect, if the attribute is not encoded as an integer
        /// \param expectval is the integer value to return if the attribute matches the expected string
        /// \return the encoded integer or the integer value associated with the expected string
        public abstract long readSignedIntegerExpectString(ref AttributeId attribId,
            ref string expect, long expectval);

        /// \brief Parse the current attribute as an unsigned integer value
        /// The last attribute, as returned by getNextAttributeId, is treated as an unsigned integer, and its value is returned.
        /// \return the unsigned integer value associated with the current attribute.
        public abstract ulong readUnsignedInteger();

        /// \brief Find and parse a specific attribute in the current element as an unsigned integer
        /// The set of attributes for the current element is searched for a match to the given attribute id.
        /// This attribute is then parsed as an unsigned integer and its value returned.
        /// If there is no attribute matching the id, an exception is thrown.
        /// Parsing via getNextAttributeId is reset.
        /// \param attribId is the specific attribute id to match
        /// \return the unsigned integer value
        public abstract ulong readUnsignedInteger(AttributeId attribId);

        /// \brief Parse the current attribute as a string
        /// The last attribute, as returned by getNextAttributeId, is returned as a string.
        /// \return the string associated with the current attribute.
        public abstract string readString();

        /// \brief Find the specific attribute in the current element and return it as a string
        /// The set of attributes for the current element is searched for a match to the given attribute id.
        /// This attribute is then returned as a string.  If there is no attribute matching the id, and exception is thrown.
        /// Parse via getNextAttributeId is reset.
        /// \param attribId is the specific attribute id to match
        /// \return the string associated with the attribute
        public abstract string readString(AttributeId attribId);

        /// \brief Parse the current attribute as an address space
        /// The last attribute, as returned by getNextAttributeId, is returned as an address space.
        /// \return the address space associated with the current attribute.
        public abstract AddrSpace readSpace();

        /// \brief Find the specific attribute in the current element and return it as an address space
        /// Search attributes from the current element for a match to the given attribute id.
        /// Return this attribute as an address space. If there is no attribute matching the id, an exception is thrown.
        /// Parse via getNextAttributeId is reset.
        /// \param attribId is the specific attribute id to match
        /// \return the address space associated with the attribute
        public abstract AddrSpace readSpace(AttributeId attribId);

        /// \brief Skip parsing of the next element
        /// The element skipped is the one that would be opened by the next call to openElement.
        public void skipElement()
        {
            uint elemId = openElement();
            closeElementSkipping(elemId);
        }
    }
}
