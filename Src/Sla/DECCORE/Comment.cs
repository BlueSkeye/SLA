using ghidra;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Runtime.Intrinsics.X86;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Sla.DECCORE
{
    /// \brief A comment attached to a specific function and code address
    /// Things contains the actual character data of the comment. It is
    /// fundamentally attached to a specific function and to the address of
    /// an instruction (within the function's body). Comments
    /// can be categorized as a \e header (or not) depending on whether
    /// it should be displayed as part of the general description of the
    /// function or not. Other properties can be assigned to a comment, to
    /// allow the user to specify the subset of all comments they want to display.
    internal class Comment
    {
        // friend class CommentDatabaseInternal;
        /// The properties associated with the comment
        private comment_type type;
        /// Sub-identifier for uniqueness
        internal int uniq;
        /// Address of the function containing the comment
        private Address funcaddr;
        /// Address associated with the comment
        private Address addr;
        /// The body of the comment
        private string text;
        /// \b true if this comment has already been emitted
        private /*mutable*/ bool emitted;

        /// \brief Possible properties associated with a comment
        public enum comment_type
        {
            user1 = 1,          ///< The first user defined property
            user2 = 2,          ///< The second user defined property
            user3 = 4,          ///< The third user defined property
            header = 8,         ///< The comment should be displayed in the function header
            warning = 16,       ///< The comment is auto-generated to alert the user
            warningheader = 32      ///< The comment is auto-generated and should be in the header
        }

        /// Constructor for use with decode
        public Comment()
        {
        }

        /// Constructor
        /// \param tp is the set of properties to associate with the comment (or 0 for no properties)
        /// \param fad is the Address of the function containing the comment
        /// \param ad is the Address of the instruction associated with the comment
        /// \param uq is used internally to sub-sort comments at the same address
        /// \param txt is the body of the comment
        public Comment(uint tp, Address fad, Address ad, int uq, string txt)
        {
            type = tp;
            uniq = uq;
            funcaddr = fad;
            addr = ad;
            text = txt;
            emitted = false;
        }

        /// Mark that \b this comment has been emitted
        public void setEmitted(bool val)
        {
            emitted = val;
        }

        /// Return \b true if \b this comment is already emitted
        public bool isEmitted()
        {
            return emitted;
        }

        /// Get the properties associated with the comment
        public comment_type getType()
        {
            return type;
        }

        /// Get the address of the function containing the comment
        public Address getFuncAddr()
        {
            return funcaddr;
        }

        /// Get the address to which the instruction is attached
        public Address getAddr()
        {
            return addr;
        }

        /// Get the sub-sorting index
        public int getUniq()
        {
            return uniq;
        }

        /// Get the body of the comment
        public string getText()
        {
            return text;
        }

        /// Encode the comment to a stream
        /// The single comment is encoded as a \<comment> element.
        /// \param encoder is the stream encoder
        public void encode(Encoder encoder)
        {
            string tpname = Comment.decodeCommentType(type);

            encoder.openElement(ElementId.ELEM_COMMENT);
            encoder.writeString(AttributeId.ATTRIB_TYPE, tpname);
            encoder.openElement(ElementId.ELEM_ADDR);
            funcaddr.getSpace().encodeAttributes(encoder, funcaddr.getOffset());
            encoder.closeElement(ElementId.ELEM_ADDR);
            encoder.openElement(ElementId.ELEM_ADDR);
            addr.getSpace().encodeAttributes(encoder, addr.getOffset());
            encoder.closeElement(ElementId.ELEM_ADDR);
            encoder.openElement(ElementId.ELEM_TEXT);
            encoder.writeString(AttributeId.ATTRIB_CONTENT, text);
            encoder.closeElement(ElementId.ELEM_TEXT);
            encoder.closeElement(ElementId.ELEM_COMMENT);
        }

        /// Restore the comment from XML
        /// Parse a \<comment> element from the given stream decoder
        /// \param decoder is the given stream decoder
        public void decode(Decoder decoder)
        {
            emitted = false;
            type = 0;
            uint elemId = decoder.openElement(ElementId.ELEM_COMMENT);
            type = Comment.encodeCommentType(decoder.readString(AttributeId.ATTRIB_TYPE));
            funcaddr = Address.decode(decoder);
            addr = Address.decode(decoder);
            uint subId = decoder.peekElement();
            if (subId != 0) {
                decoder.openElement();
                text = decoder.readString(AttributeId.ATTRIB_CONTENT);
                decoder.closeElement(subId);
            }
            decoder.closeElement(elemId);
        }

        /// Convert name string to comment property
        /// \param name is a string representation of a single comment property
        /// \return the enumerated property type
        public static comment_type encodeCommentType(string name)
        {
            switch (name)
            {
                case "user1":
                    return Comment.comment_type.user1;
                case "user2":
                    return Comment.comment_type.user2;
                case "user3":
                    return Comment.comment_type.user3;
                case "header":
                    return Comment.comment_type.header;
                case "warning":
                    return Comment.comment_type.warning;
                case "warningheader":
                    return Comment.comment_type.warningheader;
                default:
                    throw new LowlevelError($"Unknown comment type: {name}");
            }
        }

        /// Convert comment property to string
        /// \param val is a single comment property
        /// \return the string representation of the property
        public static string decodeCommentType(comment_type val)
        {
            switch (val) {
                case comment_type.user1:
                    return "user1";
                case comment_type.user2:
                    return "user2";
                case comment_type.user3:
                    return "user3";
                case comment_type.header:
                    return "header";
                case comment_type.warning:
                    return "warning";
                case comment_type.warningheader:
                    return "warningheader";
                default:
                    break;
            }
            throw new LowlevelError("Unknown comment type");
        }
    }
}
