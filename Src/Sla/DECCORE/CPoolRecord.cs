using ghidra;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief A description of a byte-code object referenced by a constant
    ///
    /// Byte-code languages can make use of objects that the \e system knows about
    /// but which aren't fully embedded in the encoding of instructions that use them.
    /// Instead the instruction refers to the object via a special encoded reference. This class
    /// describes one object described by such a reference. In order to provide a concrete
    /// interpretation of the instruction (i.e. a p-code translation), these objects generally
    /// resolve to some sort of constant value (hence the term \b constant \b pool). The type
    /// of constant goes to the formal CPoolRecord \b tag field which can be a:
    ///   - Primitive value (integer, floating-point)
    ///   - String literal (pointer to)
    ///   - Class method (pointer to)
    ///   - Class field (offset of)
    ///   - Array length
    ///   - Data-type (pointer to a descriptor)
    ///
    /// For decompilation, knowing the actual \e constant a byte-code interpreter would need
    /// is secondary to knowing what object is being referenced.  So the CPoolRecord can hold a
    /// constant value, but generally it provides a data-type associated with the object
    /// and a symbol name or other string token naming the object.
    internal class CPoolRecord
    {
        /// \brief Generic constant pool tag types
        public enum ConstantPoolTagTypes
        {
            primitive = 0,  ///< Constant \b value of data-type \b type, cpool operator can be eliminated
            string_literal = 1, ///< Constant reference to string (passed back as \b byteData)
            class_reference = 2,    ///< Reference to (system level) class object, \b token holds class name
            pointer_method = 3, ///< Pointer to a method, name in \b token, signature in \b type
            pointer_field = 4,  ///< Pointer to a field, name in \b token, data-type in \b type
            array_length = 5,   ///< Integer length, \b token is language specific indicator, \b type is integral data-type
            instance_of = 6,    ///< Boolean value, \b token is language specific indicator, \b type is boolean data-type
            check_cast = 7  ///< Pointer to object, new name in \b token, new data-type in \b type
        };
        public enum MethodType
        {
            is_constructor = 0x1,   ///< Referenced method is a constructor
            is_destructor = 0x2     ///< Referenced method is a destructor
        };

        // friend class ConstantPool;
        /// Descriptor of type of the object
        private uint tag;
        /// Additional boolean properties on the record
        private MethodType flags;
        /// Name or token associated with the object
        private string token;
        /// Constant value of the object (if known)
        private ulong value;
        /// Data-type associated with the object
        private Datatype type;
        /// For string literals, the raw byte data of the string
        private byte byteData;
        /// The number of bytes in the data for a string literal
        private int byteDataLen;

        /// Construct an empty record
        public CPoolRecord()
        {
            type = null;
            byteData = (byte*)0;
        }

        /// Destructor
        ~CPoolRecord()
        {
            if (byteData != (byte*)0) delete[] byteData;
        }

        /// Get the type of record
        public uint getTag() => tag;

        /// Get name of method or data-type
        public string getToken() => token;

        /// Get pointer to string literal data
        public byte getByteData() => byteData;

        /// Number of bytes of string literal data
        public int getByteDataLength() => byteDataLen;

        /// Get the data-type associated with \b this
        public Datatype getType() => type;

        /// Get the constant value associated with \b this
        public ulong getValue() => value;

        /// Is object a constructor method
        public bool isConstructor() => ((flags & MethodType.is_constructor)!= 0);

        /// Is object a destructor method
        public bool isDestructor() => ((flags & MethodType.is_destructor)!= 0);

        /// Encode \b this to a stream
        /// Encode the constant pool object description as a \<cpoolrec> element.
        /// \param encoder is the stream encoder
        public void encode(Encoder encoder)
        {
            encoder.openElement(ELEM_CPOOLREC);
            if (tag == pointer_method)
                encoder.writeString(ATTRIB_TAG, "method");
            else if (tag == pointer_field)
                encoder.writeString(ATTRIB_TAG, "field");
            else if (tag == instance_of)
                encoder.writeString(ATTRIB_TAG, "instanceof");
            else if (tag == array_length)
                encoder.writeString(ATTRIB_TAG, "arraylength");
            else if (tag == check_cast)
                encoder.writeString(ATTRIB_TAG, "checkcast");
            else if (tag == string_literal)
                encoder.writeString(ATTRIB_TAG, "string");
            else if (tag == class_reference)
                encoder.writeString(ATTRIB_TAG, "classref");
            else
                encoder.writeString(ATTRIB_TAG, "primitive");
            if (isConstructor())
                encoder.writeBool(ATTRIB_CONSTRUCTOR, true);
            if (isDestructor())
                encoder.writeBool(ATTRIB_DESTRUCTOR, true);
            if (tag == primitive)
            {
                encoder.openElement(ELEM_VALUE);
                encoder.writeUnsignedInteger(ATTRIB_CONTENT, value);
                encoder.closeElement(ELEM_VALUE);
            }
            if (byteData != (byte*)0)
            {
                encoder.openElement(ELEM_DATA);
                encoder.writeSignedInteger(ATTRIB_LENGTH, byteDataLen);
                int wrap = 0;
                ostringstream s;
                for (int i = 0; i < byteDataLen; ++i)
                {
                    s << setfill('0') << setw(2) << hex << byteData[i] << ' ';
                    wrap += 1;
                    if (wrap > 15)
                    {
                        s << '\n';
                        wrap = 0;
                    }
                }
                encoder.writeString(ATTRIB_CONTENT, s.str());
                encoder.closeElement(ELEM_DATA);
            }
            else
            {
                encoder.openElement(ELEM_TOKEN);
                encoder.writeString(ATTRIB_CONTENT, token);
                encoder.closeElement(ELEM_TOKEN);
            }
            type.encode(encoder);
            encoder.closeElement(ELEM_CPOOLREC);
        }

        /// Decode \b this from a stream
        /// Initialize \b this CPoolRecord instance from a \<cpoolrec> element.
        /// \param decoder is the stream decoder
        /// \param typegrp is the TypeFactory used to resolve data-types
        public void decode(Decoder decoder, TypeFactory typegrp)
        {
            tag = primitive;    // Default tag
            value = 0;
            flags = 0;
            uint elemId = decoder.openElement(ELEM_CPOOLREC);
            for (; ; )
            {
                uint attribId = decoder.getNextAttributeId();
                if (attribId == 0) break;
                if (attribId == ATTRIB_TAG)
                {
                    string tagstring = decoder.readString();
                    if (tagstring == "method")
                        tag = pointer_method;
                    else if (tagstring == "field")
                        tag = pointer_field;
                    else if (tagstring == "instanceof")
                        tag = instance_of;
                    else if (tagstring == "arraylength")
                        tag = array_length;
                    else if (tagstring == "checkcast")
                        tag = check_cast;
                    else if (tagstring == "string")
                        tag = string_literal;
                    else if (tagstring == "classref")
                        tag = class_reference;
                }
                else if (attribId == ATTRIB_CONSTRUCTOR)
                {
                    if (decoder.readBool())
                        flags |= CPoolRecord::is_constructor;
                }
                else if (attribId == ATTRIB_DESTRUCTOR)
                {
                    if (decoder.readBool())
                        flags |= CPoolRecord::is_destructor;
                }
            }
            uint subId;
            if (tag == primitive)
            {   // First tag must be value
                subId = decoder.openElement(ELEM_VALUE);
                value = decoder.readUnsignedInteger(ATTRIB_CONTENT);
                decoder.closeElement(subId);
            }
            subId = decoder.openElement();
            if (subId == ELEM_TOKEN)
                token = decoder.readString(ATTRIB_CONTENT);
            else
            {
                byteDataLen = decoder.readSignedInteger(ATTRIB_LENGTH);
                istringstream s3(decoder.readString(ATTRIB_CONTENT));
                byteData = new byte[byteDataLen];
                for (int i = 0; i < byteDataLen; ++i)
                {
                    uint val;
                    s3 >> ws >> hex >> val;
                    byteData[i] = (byte)val;
                }
            }
            decoder.closeElement(subId);
            if (tag == string_literal && (byteData == (byte*)0))
                throw new LowlevelError("Bad constant pool record: missing <data>");
            if (flags != 0)
            {
                bool isConstructor = ((flags & is_constructor) != 0);
                bool isDestructor = ((flags & is_destructor) != 0);
                type = typegrp.decodeTypeWithCodeFlags(decoder, isConstructor, isDestructor);
            }
            else
                type = typegrp.decodeType(decoder);
            decoder.closeElement(elemId);
        }
    }
}
