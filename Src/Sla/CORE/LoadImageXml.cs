using Sla.CORE;
using Sla.DECCORE;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Text;
using System.Threading.Tasks;

namespace Sla.CORE
{
    /// \brief Implementation of the LoadImage interface using underlying data stored in an XML format
    ///
    /// The image data is stored in an XML file in a \<binaryimage> file.
    /// The data is encoded in \<bytechunk> and potentially \<symbol> files.
    internal class LoadImageXml : LoadImage
    {
        private Element rootel;          ///< The root XML element
        private string archtype;                ///< The architecture string
        private AddrSpaceManager manage;     ///< Manager of addresses
        private set<Address> readonlyset;           ///< Starting address of read-only chunks
        private Dictionary<Address, List<uint1>> chunk;      ///< Chunks of image data, mapped by address
        private Dictionary<Address, string> addrtosymbol;      ///< Symbols sorted by address
        private /*mutable*/ Dictionary<Address, string>::const_iterator cursymbol; ///< Current symbol being reported

        /// Make sure every chunk is followed by at least 512 bytes of pad
        private void pad()
        {
            map<Address, List<uint1>>::iterator iter, lastiter;

            // Search for completely redundant chunks
            if (chunk.empty()) return;
            lastiter = chunk.begin();
            iter = lastiter;
            ++iter;
            while (iter != chunk.end())
            {
                if ((*lastiter).first.getSpace() == (*iter).first.getSpace())
                {
                    uintb end1 = (*lastiter).first.getOffset() + (*lastiter).second.size() - 1;
                    uintb end2 = (*iter).first.getOffset() + (*iter).second.size() - 1;
                    if (end1 >= end2)
                    {
                        chunk.erase(iter);
                        iter = lastiter;
                        ++iter;
                        continue;
                    }
                }
                lastiter = iter;
                ++iter;
            }

            iter = chunk.begin();
            while (iter != chunk.end())
            {
                Address endaddr = (*iter).first + (*iter).second.size();
                if (endaddr < (*iter).first)
                {
                    ++iter;
                    continue; // All the way to end of space
                }
                ++iter;
                int4 maxsize = 512;
                uintb room = endaddr.getSpace().getHighest() - endaddr.getOffset() + 1;
                if ((uintb)maxsize > room)
                    maxsize = (int4)room;
                if ((iter != chunk.end()) && ((*iter).first.getSpace() == endaddr.getSpace()))
                {
                    if (endaddr.getOffset() >= (*iter).first.getOffset()) continue;
                    room = (*iter).first.getOffset() - endaddr.getOffset();
                    if ((uintb)maxsize > room)
                        maxsize = (int4)room;
                }
                List<uint1> & vec(chunk[endaddr]);
                for (int4 i = 0; i < maxsize; ++i)
                    vec.push_back(0);
            }
        }

        /// \param f is the (path to the) underlying XML file
        /// \param el is the parsed form of the file
        public LoadImageXml(string f, Element el)
        {
            manage = (AddrSpaceManager*)0;
            rootel = el;

            // Extract architecture information
            if (rootel.getName() != "binaryimage")
                throw new LowlevelError("Missing binaryimage tag in " + filename);
            archtype = el.getAttributeValue("arch");
        }

        /// Read XML tags into the containers
        /// \param m is for looking up address space
        public void open(AddrSpaceManager m)
        {
            manage = m;
            uint4 sz;           // unused size

            // Read parsed xml file
            XmlDecode decoder(m, rootel);
            uint4 elemId = decoder.openElement(ELEM_BINARYIMAGE);
            for (; ; )
            {
                uint4 subId = decoder.openElement();
                if (subId == 0) break;
                if (subId == ELEM_SYMBOL)
                {
                    AddrSpace @base = decoder.readSpace(ATTRIB_SPACE);
                    Address addr(@base, @base.decodeAttributes(decoder, sz));
                    string nm = decoder.readString(ATTRIB_NAME);
                    addrtosymbol[addr] = nm;
                }
                else if (subId == ELEM_BYTECHUNK) {
                    AddrSpace @base = decoder.readSpace(ATTRIB_SPACE);
                    Address addr(@base, @base.decodeAttributes(decoder, sz));
                    map<Address, List<uint1>>::iterator chnkiter;
                    List<uint1> & vec(chunk[addr]);
                    vec.clear();
                    decoder.rewindAttributes();
                    for (; ; ) {
                        uint4 attribId = decoder.getNextAttributeId();
                        if (attribId == 0) break;
                        if (attribId == ATTRIB_READONLY)
                            if (decoder.readBool())
                                readonlyset.insert(addr);
                    }
                    istringstream @is = new istringstream(decoder.readString(ATTRIB_CONTENT));
                    int4 val;
                    char c1, c2;
                    @is >> ws;
                    c1 = @is.get();
                    c2 = @is.get();
                    while ((c1 > 0) && (c2 > 0)) {
                        if (c1 <= '9')
                            c1 = c1 - '0';
                        else if (c1 <= 'F')
                            c1 = c1 + 10 - 'A';
                        else
                            c1 = c1 + 10 - 'a';
                        if (c2 <= '9')
                            c2 = c2 - '0';
                        else if (c2 <= 'F')
                            c2 = c2 + 10 - 'A';
                        else
                            c2 = c2 + 10 - 'a';
                        val = c1 * 16 + c2;
                        vec.push_back((uint1)val);
                        @is >> ws;
                        c1 = @is.get();
                        c2 = @is.get();
                    }
                }
                else
                    throw new LowlevelError("Unknown LoadImageXml tag");
                decoder.closeElement(subId);
            }
            decoder.closeElement(elemId);
            pad();
        }

        /// Clear out all the caches
        public void clear()
        {
            archtype.clear();
            manage = (AddrSpaceManager*)0;
            chunk.clear();
            addrtosymbol.clear();
        }

        /// Encode the image to a stream
        /// Encode the byte chunks and symbols as elements
        /// \param encoder is the stream encoder
        public void encode(Encoder encoder)
        {
            encoder.openElement(ELEM_BINARYIMAGE);
            encoder.writeString(ATTRIB_ARCH, archtype);

            map<Address, List<uint1>>::const_iterator iter1;
            for (iter1 = chunk.begin(); iter1 != chunk.end(); ++iter1)
            {
                List<uint1> &vec((*iter1).second);
                if (vec.size() == 0) continue;
                encoder.openElement(ELEM_BYTECHUNK);
                (*iter1).first.getSpace().encodeAttributes(encoder, (*iter1).first.getOffset());
                if (readonlyset.find((*iter1).first) != readonlyset.end())
                    encoder.writeBool(ATTRIB_READONLY, "true");
                ostringstream s;
                s << '\n' << setfill('0');
                for (int4 i = 0; i < vec.size(); ++i)
                {
                    s << hex << setw(2) << (int4)vec[i];
                    if (i % 20 == 19)
                        s << '\n';
                }
                s << '\n';
                encoder.writeString(ATTRIB_CONTENT, s.str());
                encoder.closeElement(ELEM_BYTECHUNK);
            }

            map<Address, string>::const_iterator iter2;
            for (iter2 = addrtosymbol.begin(); iter2 != addrtosymbol.end(); ++iter2)
            {
                encoder.openElement(ELEM_SYMBOL);
                (*iter2).first.getSpace().encodeAttributes(encoder, (*iter2).first.getOffset());
                encoder.writeString(ATTRIB_NAME, (*iter2).second);
                encoder.closeElement(ELEM_SYMBOL);
            }
            encoder.closeElement(ELEM_BINARYIMAGE);
        }

        ~LoadImageXml()
        {
            clear();
        }

        public override void loadFill(byte[] ptr, int4 size, Address addr)
        {
            map<Address, List<uint1>>::const_iterator iter;
            Address curaddr;
            bool emptyhit = false;

            curaddr = addr;
            iter = chunk.upper_bound(curaddr); // First one greater than
            if (iter != chunk.begin())
                --iter;         // Last one less or equal
            while ((size > 0) && (iter != chunk.end()))
            {
                List<uint1> &chnk((*iter).second);
                int4 chnksize = chnk.size();
                int4 over = curaddr.overlap(0, (*iter).first, chnksize);
                if (over != -1)
                {
                    if (chnksize - over > size)
                        chnksize = over + size;
                    for (int4 i = over; i < chnksize; ++i)
                        *ptr++ = chnk[i];
                    size -= (chnksize - over);
                    curaddr = curaddr + (chnksize - over);
                    ++iter;
                }
                else
                {
                    emptyhit = true;
                    break;
                }
            }
            if ((size > 0) || emptyhit)
            {
                ostringstream errmsg;
                errmsg << "Bytes at ";
                curaddr.printRaw(errmsg);
                errmsg << " are not mapped";
                throw DataUnavailError(errmsg.str());
            }
        }

        public override void openSymbols()
        {
            cursymbol = addrtosymbol.begin();
        }

        public override bool getNextSymbol(LoadImageFunc record)
        {
            if (cursymbol == addrtosymbol.end()) return false;
            record.name = (*cursymbol).second;
            record.address = (*cursymbol).first;
            ++cursymbol;
            return true;
        }

        public override void getReadonly(RangeList list)
        {
            map<Address, List<uint1>>::const_iterator iter;

            // List all the readonly chunks
            for (iter = chunk.begin(); iter != chunk.end(); ++iter)
            {
                if (readonlyset.find((*iter).first) != readonlyset.end())
                {
                    List<uint1> &chnk((*iter).second);
                    uintb start = (*iter).first.getOffset();
                    uintb stop = start + chnk.size() - 1;
                    list.insertRange((*iter).first.getSpace(), start, stop);
                }
            }
        }

        public override string getArchType() => archtype;

        public override void adjustVma(long adjust)
        {
            map<Address, List<uint1>>::iterator iter1;
            map<Address, string>::iterator iter2;

            map<Address, List<uint1>> newchunk;
            map<Address, string> newsymbol;

            for (iter1 = chunk.begin(); iter1 != chunk.end(); ++iter1)
            {
                AddrSpace* spc = (*iter1).first.getSpace();
                int4 off = AddrSpace::addressToByte(adjust, spc.getWordSize());
                Address newaddr = (*iter1).first + off;
                newchunk[newaddr] = (*iter1).second;
            }
            chunk = newchunk;
            for (iter2 = addrtosymbol.begin(); iter2 != addrtosymbol.end(); ++iter2)
            {
                AddrSpace* spc = (*iter2).first.getSpace();
                int4 off = AddrSpace::addressToByte(adjust, spc.getWordSize());
                Address newaddr = (*iter2).first + off;
                newsymbol[newaddr] = (*iter2).second;
            }
            addrtosymbol = newsymbol;
        }
    }
}
