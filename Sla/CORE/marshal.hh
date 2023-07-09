/* ###
 * IP: GHIDRA
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 * 
 *      http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
#ifndef __MARSHAL_HH__
#define __MARSHAL_HH__

#include "xml.hh"
#include <list>
#include <unordered_map>

namespace ghidra {

using std::list;
using std::unordered_map;

/// \brief An annotation for a data element to being transferred to/from a stream
///
/// This class parallels the XML concept of an \b attribute on an element. An AttributeId describes
/// a particular piece of data associated with an ElementId.  The defining characteristic of the AttributeId is
/// its name.  Internally this name is associated with an integer id.  The name (and id) uniquely determine
/// the data being labeled, within the context of a specific ElementId.  Within this context, an AttributeId labels either
///   - An unsigned integer
///   - A signed integer
///   - A boolean value
///   - A string
///
/// The same AttributeId can be used to label a different type of data when associated with a different ElementId.
class AttributeId {
  static unordered_map<string,uint4> lookupAttributeId;		///< A map of AttributeId names to their associated id
  static vector<AttributeId *> &getList(void);			///< Retrieve the list of static AttributeId
  string name;			///< The name of the attribute
  uint4 id;			///< The (internal) id of the attribute
public:
  AttributeId(const string &nm,uint4 i);	///< Construct given a name and id
  const string &getName(void) const { return name; }				///< Get the attribute's name
  uint4 getId(void) const { return id; }					///< Get the attribute's id
  bool operator==(const AttributeId &op2) const { return (id == op2.id); }	///< Test equality with another AttributeId
  static uint4 find(const string &nm);			///< Find the id associated with a specific attribute name
  static void initialize(void);				///< Populate a hashtable with all AttributeId objects
  friend bool operator==(uint4 id,const AttributeId &op2) { return (id == op2.id); }	///< Test equality of a raw integer id with an AttributeId
  friend bool operator==(const AttributeId &op1,uint4 id) { return (op1.id == id); }	///< Test equality of an AttributeId with a raw integer id
};


class AddrSpace;
class AddrSpaceManager;



/// \brief An XML based decoder
///
/// The underlying transfer encoding is an XML document.  The decoder can either be initialized with an
/// existing Element as the root of the data to transfer, or the ingestStream() method can be invoked
/// to read the XML document from an input stream, in which case the decoder manages the Document object.
class XmlDecode : public Decoder {
  Document *document;				///< An ingested XML document, owned by \b this decoder
  const Element *rootElement;			///< The root XML element to be decoded
  vector<const Element *> elStack;		///< Stack of currently \e open elements
  vector<List::const_iterator> iterStack;	///< Index of next child for each \e open element
  int4 attributeIndex;				///< Position of \e current attribute to parse (in \e current element)
  int4 findMatchingAttribute(const Element *el,const string &attribName);
public:
  XmlDecode(const AddrSpaceManager *spc,const Element *root) : Decoder(spc) {
    document = (Document *)0; rootElement = root; attributeIndex = -1; }	///< Constructor with preparsed root
  XmlDecode(const AddrSpaceManager *spc) : Decoder(spc) {
    document = (Document *)0; rootElement = (const Element *)0; attributeIndex = -1; }	///< Constructor for use with ingestStream
  const Element *getCurrentXmlElement(void) const { return elStack.back(); }	///< Get pointer to underlying XML element object
  virtual ~XmlDecode(void);
  virtual void ingestStream(istream &s);
  virtual uint4 peekElement(void);
  virtual uint4 openElement(void);
  virtual uint4 openElement(const ElementId &elemId);
  virtual void closeElement(uint4 id);
  virtual void closeElementSkipping(uint4 id);
  virtual void rewindAttributes(void);
  virtual uint4 getNextAttributeId(void);
  virtual uint4 getIndexedAttributeId(const AttributeId &attribId);
  virtual bool readBool(void);
  virtual bool readBool(const AttributeId &attribId);
  virtual intb readSignedInteger(void);
  virtual intb readSignedInteger(const AttributeId &attribId);
  virtual intb readSignedIntegerExpectString(const string &expect,intb expectval);
  virtual intb readSignedIntegerExpectString(const AttributeId &attribId,const string &expect,intb expectval);
  virtual uintb readUnsignedInteger(void);
  virtual uintb readUnsignedInteger(const AttributeId &attribId);
  virtual string readString(void);
  virtual string readString(const AttributeId &attribId);
  virtual AddrSpace *readSpace(void);
  virtual AddrSpace *readSpace(const AttributeId &attribId);
};

/// \brief An XML based encoder
///
/// The underlying transfer encoding is an XML document.  The encoder is initialized with a stream which will
/// receive the XML document as calls are made on the encoder.
class XmlEncode : public Encoder {
  friend class XmlDecode;
  ostream &outStream;			///< The stream receiving the encoded data
  bool elementTagIsOpen;		///< If \b true, new attributes can be written to the current element
public:
  XmlEncode(ostream &s) : outStream(s) { elementTagIsOpen = false; } ///< Construct from a stream
  virtual void openElement(const ElementId &elemId);
  virtual void closeElement(const ElementId &elemId);
  virtual void writeBool(const AttributeId &attribId,bool val);
  virtual void writeSignedInteger(const AttributeId &attribId,intb val);
  virtual void writeUnsignedInteger(const AttributeId &attribId,uintb val);
  virtual void writeString(const AttributeId &attribId,const string &val);
  virtual void writeStringIndexed(const AttributeId &attribId,uint4 index,const string &val);
  virtual void writeSpace(const AttributeId &attribId,const AddrSpace *spc);
};

/// \brief Protocol format for PackedEncode and PackedDecode classes
///
/// All bytes in the encoding are expected to be non-zero.  Element encoding looks like
///   - 01xiiiii is an element start
///   - 10xiiiii is an element end
///   - 11xiiiii is an attribute start
///
/// Where iiiii is the (first) 5 bits of the element/attribute id.
/// If x=0, the id is complete.  If x=1, the next byte contains 7 more bits of the id:  1iiiiiii
///
/// After an attribute start, there follows a \e type byte:  ttttllll, where the first 4 bits indicate the
/// type of attribute and final 4 bits are a \b length \b code.  The types are:
///   - 1 = boolean (lengthcode=0 for false, lengthcode=1 for true)
///   - 2 = positive signed integer
///   - 3 = negative signed integer (stored in negated form)
///   - 4 = unsigned integer
///   - 5 = basic address space (encoded as the integer index of the space)
///   - 6 = special address space (lengthcode 0=>stack 1=>join 2=>fspec 3=>iop)
///   - 7 = string
///
/// All attribute types except \e boolean and \e special, have an encoded integer after the \e type byte.
/// The \b length \b code, indicates the number bytes used to encode the integer, 7-bits of info per byte, 1iiiiiii.
/// A \b length \b code of zero is used to encode an integer value of 0, with no following bytes.
///
/// For strings, the integer encoded after the \e type byte, is the actual length of the string.  The
/// string data itself is stored immediately after the length integer using UTF8 format.
namespace PackedFormat {
  static const uint1 HEADER_MASK = 0xc0;		///< Bits encoding the record type
  static const uint1 ELEMENT_START = 0x40;		///< Header for an element start record
  static const uint1 ELEMENT_END = 0x80;		///< Header for an element end record
  static const uint1 ATTRIBUTE = 0xc0;			///< Header for an attribute record
  static const uint1 HEADEREXTEND_MASK = 0x20;		///< Bit indicating the id extends into the next byte
  static const uint1 ELEMENTID_MASK = 0x1f;		///< Bits encoding (part of) the id in the record header
  static const uint1 RAWDATA_MASK = 0x7f;		///< Bits of raw data in follow-on bytes
  static const int4 RAWDATA_BITSPERBYTE = 7;		///< Number of bits used in a follow-on byte
  static const uint1 RAWDATA_MARKER = 0x80;		///< The unused bit in follow-on bytes. (Always set to 1)
  static const int4 TYPECODE_SHIFT = 4;			///< Bit position of the type code in the type byte
  static const uint1 LENGTHCODE_MASK = 0xf;		///< Bits in the type byte forming the length code
  static const uint1 TYPECODE_BOOLEAN = 1;		///< Type code for the \e boolean type
  static const uint1 TYPECODE_SIGNEDINT_POSITIVE = 2;	///< Type code for the \e signed \e positive \e integer type
  static const uint1 TYPECODE_SIGNEDINT_NEGATIVE = 3;	///< Type code for the \e signed \e negative \e integer type
  static const uint1 TYPECODE_UNSIGNEDINT = 4;		///< Type code for the \e unsigned \e integer type
  static const uint1 TYPECODE_ADDRESSSPACE = 5;		///< Type code for the \e address \e space type
  static const uint1 TYPECODE_SPECIALSPACE = 6;		///< Type code for the \e special \e address \e space type
  static const uint1 TYPECODE_STRING = 7;		///< Type code for the \e string type
  static const uint4 SPECIALSPACE_STACK = 0;		///< Special code for the \e stack space
  static const uint4 SPECIALSPACE_JOIN = 1;		///< Special code for the \e join space
  static const uint4 SPECIALSPACE_FSPEC = 2;		///< Special code for the \e fspec space
  static const uint4 SPECIALSPACE_IOP = 3;		///< Special code for the \e iop space
  static const uint4 SPECIALSPACE_SPACEBASE = 4;	///< Special code for a \e spacebase space
}

/// \brief A byte-based decoder designed to marshal info to the decompiler efficiently
///
/// The decoder expects an encoding as described in PackedFormat.  When ingested, the stream bytes are
/// held in a sequence of arrays (ByteChunk). During decoding, \b this object maintains a Position in the
/// stream at the start and end of the current open element, and a Position of the next attribute to read to
/// facilitate getNextAttributeId() and associated read*() methods.
class PackedDecode : public Decoder {
public:
  static const int4 BUFFER_SIZE;	///< The size, in bytes, of a single cached chunk of the input stream
private:
  /// \brief A bounded array of bytes
  class ByteChunk {
    friend class PackedDecode;
    uint1 *start;			///< Start of the byte array
    uint1 *end;				///< End of the byte array
  public:
    ByteChunk(uint1 *s,uint1 *e) { start = s; end = e; }	///< Constructor
  };
  /// \brief An iterator into input stream
  class Position {
    friend class PackedDecode;
    list<ByteChunk>::const_iterator seqIter;	///< Current byte sequence
    uint1 *current;				///< Current position in sequence
    uint1 *end;					///< End of current sequence
  };
  list<ByteChunk> inStream;		///< Incoming raw data as a sequence of byte arrays
  Position startPos;			///< Position at the start of the current open element
  Position curPos;			///< Position of the next attribute as returned by getNextAttributeId
  Position endPos;			///< Ending position after all attributes in current open element
  bool attributeRead;			///< Has the last attribute returned by getNextAttributeId been read
  uint1 getByte(Position &pos) { return *pos.current; }	///< Get the byte at the current position, do not advance
  uint1 getBytePlus1(Position &pos);	///< Get the byte following the current byte, do not advance position
  uint1 getNextByte(Position &pos);	///< Get the byte at the current position and advance to the next byte
  void advancePosition(Position &pos,int4 skip);	///< Advance the position by the given number of bytes
  uint8 readInteger(int4 len);		///< Read an integer from the \e current position given its length in bytes
  uint4 readLengthCode(uint1 typeByte) { return ((uint4)typeByte & PackedFormat::LENGTHCODE_MASK); }	///< Extract length code from type byte
  void findMatchingAttribute(const AttributeId &attribId);	///< Find attribute matching the given id in open element
  void skipAttribute(void);		///< Skip over the attribute at the current position
  void skipAttributeRemaining(uint1 typeByte);	///< Skip over remaining attribute data, after a mismatch
public:
  PackedDecode(const AddrSpaceManager *spcManager) : Decoder(spcManager) {}	///< Constructor
  virtual ~PackedDecode(void);
  virtual void ingestStream(istream &s);
  virtual uint4 peekElement(void);
  virtual uint4 openElement(void);
  virtual uint4 openElement(const ElementId &elemId);
  virtual void closeElement(uint4 id);
  virtual void closeElementSkipping(uint4 id);
  virtual void rewindAttributes(void);
  virtual uint4 getNextAttributeId(void);
  virtual uint4 getIndexedAttributeId(const AttributeId &attribId);
  virtual bool readBool(void);
  virtual bool readBool(const AttributeId &attribId);
  virtual intb readSignedInteger(void);
  virtual intb readSignedInteger(const AttributeId &attribId);
  virtual intb readSignedIntegerExpectString(const string &expect,intb expectval);
  virtual intb readSignedIntegerExpectString(const AttributeId &attribId,const string &expect,intb expectval);
  virtual uintb readUnsignedInteger(void);
  virtual uintb readUnsignedInteger(const AttributeId &attribId);
  virtual string readString(void);
  virtual string readString(const AttributeId &attribId);
  virtual AddrSpace *readSpace(void);
  virtual AddrSpace *readSpace(const AttributeId &attribId);
};

/// \brief A byte-based encoder designed to marshal from the decompiler efficiently
///
/// See PackedDecode for details of the encoding format.
class PackedEncode : public Encoder {
  ostream &outStream;			///< The stream receiving the encoded data
  void writeHeader(uint1 header,uint4 id);	///< Write a header, element or attribute, to stream
  void writeInteger(uint1 typeByte,uint8 val);	///< Write an integer value to the stream
public:
  PackedEncode(ostream &s) : outStream(s) {} ///< Construct from a stream
  virtual void openElement(const ElementId &elemId);
  virtual void closeElement(const ElementId &elemId);
  virtual void writeBool(const AttributeId &attribId,bool val);
  virtual void writeSignedInteger(const AttributeId &attribId,intb val);
  virtual void writeUnsignedInteger(const AttributeId &attribId,uintb val);
  virtual void writeString(const AttributeId &attribId,const string &val);
  virtual void writeStringIndexed(const AttributeId &attribId,uint4 index,const string &val);
  virtual void writeSpace(const AttributeId &attribId,const AddrSpace *spc);
};

/// An exception is thrown if the position currently points to the last byte in the stream
/// \param pos is the position in the stream to look ahead from
/// \return the next byte
inline uint1 PackedDecode::getBytePlus1(Position &pos)

{
  uint1 *ptr = pos.current + 1;
  if (ptr == pos.end) {
    list<ByteChunk>::const_iterator iter = pos.seqIter;
    ++iter;
    if (iter == inStream.end())
      throw DecoderError("Unexpected end of stream");
    ptr = (*iter).start;
  }
  return *ptr;
}

/// An exception is thrown if there are no additional bytes in the stream
/// \param pos is the position of the byte
/// \return the byte at the current position
inline uint1 PackedDecode::getNextByte(Position &pos)

{
  uint1 res = *pos.current;
  pos.current += 1;
  if (pos.current != pos.end)
    return res;
  ++pos.seqIter;
  if (pos.seqIter == inStream.end())
    throw DecoderError("Unexpected end of stream");
  pos.current = (*pos.seqIter).start;
  pos.end = (*pos.seqIter).end;
  return res;
}

/// An exception is thrown of position is advanced past the end of the stream
/// \param pos is the position being advanced
/// \param skip is the number of bytes to advance
inline void PackedDecode::advancePosition(Position &pos,int4 skip)

{
  while(pos.end - pos.current <= skip) {
    skip -= (pos.end - pos.current);
    ++pos.seqIter;
    if (pos.seqIter == inStream.end())
      throw DecoderError("Unexpected end of stream");
    pos.current = (*pos.seqIter).start;
    pos.end = (*pos.seqIter).end;
  }
  pos.current += skip;
}


extern ElementId ELEM_UNKNOWN;		///< Special element to represent an element with an unrecognized name
extern AttributeId ATTRIB_UNKNOWN;	///< Special attribute  to represent an attribute with an unrecognized name
extern AttributeId ATTRIB_CONTENT;	///< Special attribute for XML text content of an element



extern AttributeId ATTRIB_ALIGN;	///< Marshaling attribute "align"
extern AttributeId ATTRIB_BIGENDIAN;	///< Marshaling attribute "bigendian"
extern AttributeId ATTRIB_CONSTRUCTOR;	///< Marshaling attribute "constructor"
extern AttributeId ATTRIB_DESTRUCTOR;	///< Marshaling attribute "destructor"
extern AttributeId ATTRIB_EXTRAPOP;	///< Marshaling attribute "extrapop"
extern AttributeId ATTRIB_FORMAT;	///< Marshaling attribute "format"
extern AttributeId ATTRIB_HIDDENRETPARM;	///< Marshaling attribute "hiddenretparm"
extern AttributeId ATTRIB_ID;		///< Marshaling attribute "id"
extern AttributeId ATTRIB_INDEX;	///< Marshaling attribute "index"
extern AttributeId ATTRIB_INDIRECTSTORAGE;	///< Marshaling attribute "indirectstorage"
extern AttributeId ATTRIB_METATYPE;	///< Marshaling attribute "metatype"
extern AttributeId ATTRIB_MODEL;	///< Marshaling attribute "model"
extern AttributeId ATTRIB_NAME;		///< Marshaling attribute "name"
extern AttributeId ATTRIB_NAMELOCK;	///< Marshaling attribute "namelock"
extern AttributeId ATTRIB_OFFSET;	///< Marshaling attribute "offset"
extern AttributeId ATTRIB_READONLY;	///< Marshaling attribute "readonly"
extern AttributeId ATTRIB_REF;		///< Marshaling attribute "ref"
extern AttributeId ATTRIB_SIZE;		///< Marshaling attribute "size"
extern AttributeId ATTRIB_SPACE;	///< Marshaling attribute "space"
extern AttributeId ATTRIB_THISPTR;	///< Marshaling attribute "thisptr"
extern AttributeId ATTRIB_TYPE;		///< Marshaling attribute "type"
extern AttributeId ATTRIB_TYPELOCK;	///< Marshaling attribute "typelock"
extern AttributeId ATTRIB_VAL;		///< Marshaling attribute "val"
extern AttributeId ATTRIB_VALUE;	///< Marshaling attribute "value"
extern AttributeId ATTRIB_WORDSIZE;	///< Marshaling attribute "wordsize"

extern ElementId ELEM_DATA;		///< Marshaling element \<data>
extern ElementId ELEM_INPUT;		///< Marshaling element \<input>
extern ElementId ELEM_OFF;		///< Marshaling element \<off>
extern ElementId ELEM_OUTPUT;		///< Marshaling element \<output>
extern ElementId ELEM_RETURNADDRESS;	///< Marshaling element \<returnaddress>
extern ElementId ELEM_SYMBOL;		///< Marshaling element \<symbol>
extern ElementId ELEM_TARGET;		///< Marshaling element \<target>
extern ElementId ELEM_VAL;		///< Marshaling element \<val>
extern ElementId ELEM_VALUE;		///< Marshaling element \<value>
extern ElementId ELEM_VOID;		///< Marshaling element \<void>

} // End namespace ghidra
#endif
