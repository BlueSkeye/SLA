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
        private HashSet<Address> readonlyset = new HashSet<Address>(); ///< Starting address of read-only chunks
        private Dictionary<Address, List<byte>> chunk;      ///< Chunks of image data, mapped by address
        private Dictionary<Address, string> addrtosymbol;      ///< Symbols sorted by address
        /// Current symbol being reported. Reset to null pointer when enumeration end is reached.
        private /*mutable*/ Dictionary<Address, string>.Enumerator? cursymbol = null;

        /// Make sure every chunk is followed by at least 512 bytes of pad
        private void pad()
        {
            Dictionary<Address, List<byte>>.Enumerator iter, lastiter;

            // Search for completely redundant chunks
            if (0 == chunk.Count) return;
            lastiter = chunk.begin();
            iter = lastiter;
            ++iter;
            while (iter != chunk.end())
            {
                if ((*lastiter).first.getSpace() == iter.Current.Key.getSpace())
                {
                    ulong end1 = (*lastiter).first.getOffset() + (*lastiter).second.size() - 1;
                    ulong end2 = iter.Current.Key.getOffset() + (*iter).second.size() - 1;
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
                Address endaddr = iter.Current.Key + (*iter).second.size();
                if (endaddr < iter.Current.Key)
                {
                    ++iter;
                    continue; // All the way to end of space
                }
                ++iter;
                int maxsize = 512;
                ulong room = endaddr.getSpace().getHighest() - endaddr.getOffset() + 1;
                if ((ulong)maxsize > room)
                    maxsize = (int)room;
                if ((iter != chunk.end()) && (iter.Current.Key.getSpace() == endaddr.getSpace()))
                {
                    if (endaddr.getOffset() >= iter.Current.Key.getOffset()) continue;
                    room = iter.Current.Key.getOffset() - endaddr.getOffset();
                    if ((ulong)maxsize > room)
                        maxsize = (int)room;
                }
                List<byte> & vec(chunk[endaddr]);
                for (int i = 0; i < maxsize; ++i)
                    vec.Add(0);
            }
        }

        /// \param f is the (path to the) underlying XML file
        /// \param el is the parsed form of the file
        public LoadImageXml(string f, Element el)
        {
            manage = (AddrSpaceManager)null;
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
            uint sz;           // unused size

            // Read parsed xml file
            XmlDecode decoder = new XmlDecode(m, rootel);
            uint elemId = decoder.openElement(ElementId.ELEM_BINARYIMAGE);
            while (true) {
                uint subId = decoder.openElement();
                if (subId == 0) break;
                if (subId == ElementId.ELEM_SYMBOL) {
                    AddrSpace @base = decoder.readSpace(AttributeId.ATTRIB_SPACE);
                    Address addr = new Address(@base, @base.decodeAttributes(decoder, out sz));
                    string nm = decoder.readString(AttributeId.ATTRIB_NAME);
                    addrtosymbol[addr] = nm;
                }
                else if (subId == ElementId.ELEM_BYTECHUNK) {
                    AddrSpace @base = decoder.readSpace(AttributeId.ATTRIB_SPACE);
                    Address addr = new Address(@base, @base.decodeAttributes(decoder, out sz));
                    Dictionary<Address, List<byte>>.Enumerator chnkiter;
                    List<byte> vec = chunk[addr];
                    vec.Clear();
                    decoder.rewindAttributes();
                    while (true) {
                        uint attribId = decoder.getNextAttributeId();
                        if (attribId == 0) break;
                        if (attribId == AttributeId.ATTRIB_READONLY)
                            if (decoder.readBool())
                                readonlyset.insert(addr);
                    }
                    StringReader @is = new StringReader(decoder.readString(AttributeId.ATTRIB_CONTENT));
                    int val;
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
                        vec.Add((byte)val);
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
            archtype.Clear();
            manage = (AddrSpaceManager)null;
            chunk.Clear();
            addrtosymbol.Clear();
        }

        /// Encode the image to a stream
        /// Encode the byte chunks and symbols as elements
        /// \param encoder is the stream encoder
        public void encode(Sla.CORE.Encoder encoder)
        {
            encoder.openElement(ElementId.ELEM_BINARYIMAGE);
            encoder.writeString(AttributeId.ATTRIB_ARCH, archtype);

            Dictionary<Address, List<byte>>.Enumerator iter1;
            foreach (KeyValuePair<Address, List<byte>>  pair in chunk) {
                List<byte> vec = pair.Value;
                if (vec.size() == 0) continue;
                encoder.openElement(ElementId.ELEM_BYTECHUNK);
                pair.Key.getSpace().encodeAttributes(encoder, pair.Key.getOffset());
                if (readonlyset.find(pair.Key) != readonlyset.end())
                    encoder.writeBool(AttributeId.ATTRIB_READONLY, "true");
                StringWriter s = new StringWriter();
                s.WriteLine();
                for (int i = 0; i < vec.size(); ++i) {
                    s.Write($"{(int)vec[i]:X02}");
                    if (i % 20 == 19)
                        s.WriteLine();
                }
                s.WriteLine();
                encoder.writeString(AttributeId.ATTRIB_CONTENT, s.ToString());
                encoder.closeElement(ElementId.ELEM_BYTECHUNK);
            }

            Dictionary<Address, string>.Enumerator iter2;
            foreach (KeyValuePair<Address, string>  pair in addrtosymbol) {
                encoder.openElement(ElementId.ELEM_SYMBOL);
                pair.Key.getSpace().encodeAttributes(encoder, pair.Key.getOffset());
                encoder.writeString(AttributeId.ATTRIB_NAME, pair.Value);
                encoder.closeElement(ElementId.ELEM_SYMBOL);
            }
            encoder.closeElement(ElementId.ELEM_BINARYIMAGE);
        }

        ~LoadImageXml()
        {
            clear();
        }

        public override void loadFill(byte[] ptr, int size, Address addr)
        {
            Address curaddr;
            bool emptyhit = false;

            curaddr = addr;
            // First one greater than
            Dictionary<Address, List<byte>>.Enumerator iter = chunk.upper_bound(curaddr);
            if (iter != chunk.begin())
                --iter;         // Last one less or equal
                while ((size > 0) && (iter != chunk.end())) {
                List<byte> chnk = iter.Current.Value;
                int chnksize = chnk.Count;
                int over = curaddr.overlap(0, iter.Current.Key, chnksize);
                if (over != -1) {
                    if (chnksize - over > size)
                        chnksize = over + size;
                    for (int i = over; i < chnksize; ++i)
                        *ptr++ = chnk[i];
                    size -= (chnksize - over);
                    curaddr = curaddr + (chnksize - over);
                    if (!iter.MoveNext()) {
                        break;
                    }
                }
                else {
                    emptyhit = true;
                    break;
                }
            }
            if ((size > 0) || emptyhit) {
                StringWriter errmsg = new StringWriter();
                errmsg.Write("Bytes at ");
                curaddr.printRaw(errmsg);
                errmsg.Write(" are not mapped");
                throw new DataUnavailError(errmsg.ToString());
            }
        }

        public override void openSymbols()
        {
            cursymbol = addrtosymbol.GetEnumerator();
        }

        public override bool getNextSymbol(LoadImageFunc record)
        {
            if (null == cursymbol) return false;
            record.name = cursymbol.Value.Current.Value;
            record.address = cursymbol.Value.Current.Key;
            if (!cursymbol.Value.MoveNext()) {
                cursymbol.Value.Dispose();
                cursymbol = null;
            }
            return true;
        }

        public override void getReadonly(RangeList list)
        {
            Dictionary<Address, List<byte>>.Enumerator iter = chunk.GetEnumerator();

            // List all the readonly chunks
            while(iter.MoveNext()) {
                if (readonlyset.find(iter.Current.Key) != readonlyset.end()) {
                    List<byte> chnk = iter.Current.Value;
                    ulong start = iter.Current.Key.getOffset();
                    ulong stop = (ulong)(start + (uint)chnk.Count - 1);
                    list.insertRange(iter.Current.Key.getSpace(), start, stop);
                }
            }
        }

        public override string getArchType() => archtype;

        public override void adjustVma(ulong adjust)
        {
            Dictionary<Address, List<byte>>.Enumerator iter1 = chunk.GetEnumerator();
            Dictionary<Address, List<byte>> newchunk = new Dictionary<Address, List<byte>>();

            while (iter1.MoveNext()) {
                AddrSpace spc = iter1.Current.Key.getSpace();
                uint off = (uint)AddrSpace.addressToByte(adjust, spc.getWordSize());
                Address newaddr = iter1.Current.Key + off;
                newchunk[newaddr] = iter1.Current.Value;
            }
            chunk = newchunk;
            Dictionary<Address, string>.Enumerator iter2 = addrtosymbol.GetEnumerator();
            Dictionary<Address, string> newsymbol = new Dictionary<Address, string>();
            while (iter2.MoveNext()) {
                AddrSpace spc = iter2.Current.Key.getSpace();
                uint off = (uint)AddrSpace.addressToByte(adjust, spc.getWordSize());
                Address newaddr = iter2.Current.Key + off;
                newsymbol[newaddr] = iter2.Current.Value;
            }
            addrtosymbol = newsymbol;
        }
    }
}
