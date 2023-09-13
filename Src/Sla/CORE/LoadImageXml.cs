using Sla.DECCORE;

namespace Sla.CORE
{
    // \brief Implementation of the LoadImage interface using underlying data stored in
    // an XML format
    // The image data is stored in an XML file in a \<binaryimage> file.
    // The data is encoded in \<bytechunk> and potentially \<symbol> files.
    internal class LoadImageXml : LoadImage
    {
        // The root XML element
        private Element rootel;
        // The architecture string
        private string archtype;
        // Manager of addresses
        private AddrSpaceManager manage;
        // Starting address of read-only chunks
        private HashSet<Address> readonlyset = new HashSet<Address>();
        // Chunks of image data, mapped by sorted address
        private SortedList<Address, List<byte>> chunk = new SortedList<Address, List<byte>>();
        // Symbols sorted by address
        private Dictionary<Address, string> addrtosymbol = new Dictionary<Address, string>();
        /// Current symbol being reported. Reset to null pointer when enumeration end is reached.
        private /*mutable*/ Dictionary<Address, string>.Enumerator? cursymbol = null;

        // Make sure every chunk is followed by at least 512 bytes of pad
        private void pad()
        {
            // Search for completely redundant chunks
            if (0 == chunk.Count) return;
            IEnumerator<KeyValuePair<Address, List<byte>>> iter = chunk.GetEnumerator();
            List<Address> removeList = new List<Address>();
            if (iter.MoveNext()) {
                Address iteredAddress = iter.Current.Key;
                List<byte> iteredAddressChunk = iter.Current.Value;
                while (iter.MoveNext()) {
                    if (iteredAddress.getSpace() == iter.Current.Key.getSpace()) {
                        ulong end1 = iteredAddress.getOffset() + (uint)iteredAddressChunk.size() - 1;
                        ulong end2 = iter.Current.Key.getOffset() + (uint)iter.Current.Value.size() - 1;
                        if (end1 >= end2) {
                            // Scanned item is embedded in itered chunk
                            removeList.Add(iter.Current.Key);
                            continue;
                        }
                    }
                    iteredAddress = iter.Current.Key;
                    iteredAddressChunk = iter.Current.Value;
                }
                foreach (Address removedAddress in removeList) {
                    chunk.Remove(removedAddress);
                }
            }

            iter = chunk.GetEnumerator();
            while (iter.MoveNext()) {
                Address endaddr = iter.Current.Key + iter.Current.Value.size();
                if (endaddr < iter.Current.Key) {
                    // All the way to end of space
                    continue;
                }
                int maxsize = 512;
                ulong room = endaddr.getSpace().getHighest() - endaddr.getOffset() + 1;
                if ((ulong)maxsize > room)
                    maxsize = (int)room;
                if (   (iter.MoveNext())
                    && (iter.Current.Key.getSpace() == endaddr.getSpace()))
                {
                    if (endaddr.getOffset() >= iter.Current.Key.getOffset()) continue;
                    room = iter.Current.Key.getOffset() - endaddr.getOffset();
                    if ((ulong)maxsize > room)
                        maxsize = (int)room;
                }
                List<byte> vec = chunk[endaddr];
                for (int i = 0; i < maxsize; ++i) {
                    vec.Add(0);
                }
            }
        }

        // \param f is the (path to the) underlying XML file
        // \param el is the parsed form of the file
        public LoadImageXml(string f, Element el)
            :base(f)
        {
            manage = (AddrSpaceManager)null;
            rootel = el;

            // Extract architecture information
            if (rootel.getName() != "binaryimage") {
                throw new LowlevelError("Missing binaryimage tag in " + filename);
            }
            archtype = el.getAttributeValue("arch");
        }

        // Read XML tags into the containers
        // \param m is for looking up address space
        public void open(AddrSpaceManager m)
        {
            manage = m;
            // unused size
            uint sz;

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
                    List<byte> vec = chunk[addr];
                    vec.Clear();
                    decoder.rewindAttributes();
                    while (true) {
                        uint attribId = decoder.getNextAttributeId();
                        if (attribId == 0) break;
                        if (attribId == AttributeId.ATTRIB_READONLY)
                            if (decoder.readBool())
                                readonlyset.Add(addr);
                    }
                    TextReader @is = new StringReader(decoder.readString(AttributeId.ATTRIB_CONTENT));
                    int val;
                    @is.ReadSpaces();
                    char? c1 = @is.ReadCharacter();
                    char? c2 = @is.ReadCharacter();
                    while ((c1 != null) && (c1 > 0) && (c2 != null) && (c2 > 0)) {
                        byte firstDigit;
                        if (c1 <= '9')
                            firstDigit = (byte)(c1 - '0');
                        else if (c1 <= 'F')
                            firstDigit = (byte)(c1 + 10 - 'A');
                        else
                            firstDigit = (byte)(c1 + 10 - 'a');
                        byte secondDigit;
                        if (c2 <= '9')
                            secondDigit = (byte)(c2 - '0');
                        else if (c2 <= 'F')
                            secondDigit = (byte)(c2 + 10 - 'A');
                        else
                            secondDigit = (byte)(c2 + 10 - 'a');
                        val = (c1 ?? throw new ApplicationException()) * 16 +
                            c2 ?? throw new ApplicationException();
                        vec.Add((byte)val);
                        @is.ReadSpaces();
                        c1 = @is.ReadCharacter();
                        c2 = @is.ReadCharacter();
                    }
                }
                else {
                    throw new LowlevelError("Unknown LoadImageXml tag");
                }
                decoder.closeElement(subId);
            }
            decoder.closeElement(elemId);
            pad();
        }

        /// Clear out all the caches
        public void clear()
        {
            archtype = string.Empty;
            manage = (AddrSpaceManager)null;
            chunk.Clear();
            addrtosymbol.Clear();
        }

        // Encode the image to a stream
        // Encode the byte chunks and symbols as elements
        // \param encoder is the stream encoder
        public void encode(Sla.CORE.Encoder encoder)
        {
            encoder.openElement(ElementId.ELEM_BINARYIMAGE);
            encoder.writeString(AttributeId.ATTRIB_ARCH, archtype);

            foreach (KeyValuePair<Address, List<byte>>  pair in chunk) {
                List<byte> vec = pair.Value;
                if (vec.size() == 0) continue;
                encoder.openElement(ElementId.ELEM_BYTECHUNK);
                pair.Key.getSpace().encodeAttributes(encoder, pair.Key.getOffset());
                if (readonlyset.Contains(pair.Key)) {
                    encoder.writeBool(AttributeId.ATTRIB_READONLY, true);
                }
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

        public override void loadFill(byte[] ptr, int index, int size, Address addr)
        {
            Address curaddr;
            bool emptyhit = false;

            curaddr = addr;
            // First one greater than
            int iter = chunk.upper_bound(curaddr);
            // SortedDictionary<Address, List<byte>>.Enumerator iter = chunk.upper_bound(curaddr);
            if (iter != 0)
                // Last one less or equal
                --iter;
            while ((size > 0) && (iter < chunk.Count)) {
                KeyValuePair<Address, List<byte>> currentElement = chunk.ElementAt(iter);
                List<byte> chnk = currentElement.Value;
                int chnksize = chnk.Count;
                int over = curaddr.overlap(0, currentElement.Key, chnksize);
                if (over != -1) {
                    if (chnksize - over > size) {
                        chnksize = over + size;
                    }
                    for (int i = over; i < chnksize; ++i) {
                        ptr[index++] = chnk[i];
                    }
                    size -= (chnksize - over);
                    curaddr = curaddr + (chnksize - over);
                    if (chunk.Count <= (++iter)) {
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
            // SortedDictionary<Address, List<byte>>.Enumerator iter = chunk.GetEnumerator();
            int iter = 0;

            // List all the readonly chunks
            while (iter < chunk.Count) {
                KeyValuePair<Address, List<byte>> currentChunk = chunk.ElementAt(iter);
                if (readonlyset.Contains(currentChunk.Key)) {
                    List<byte> chnk = currentChunk.Value;
                    ulong start = currentChunk.Key.getOffset();
                    ulong stop = (ulong)(start + (uint)chnk.Count - 1);
                    list.insertRange(currentChunk.Key.getSpace(), start, stop);
                }
                iter++;
            }
        }

        public override string getArchType() => archtype;

        public override void adjustVma(ulong adjust)
        {
            // SortedDictionary<Address, List<byte>>.Enumerator iter1 = chunk.GetEnumerator();
            int iter1 = 0;
            SortedList<Address, List<byte>> newchunk =
                new SortedList<Address, List<byte>>();

            while (iter1 < chunk.Count) {
                KeyValuePair<Address, List<byte>> currentChunk = chunk.ElementAt(iter1);
                AddrSpace spc = currentChunk.Key.getSpace();
                uint off = (uint)AddrSpace.addressToByte(adjust, spc.getWordSize());
                Address newaddr = currentChunk.Key + (int)off;
                newchunk[newaddr] = currentChunk.Value;
                iter1++;
            }
            chunk = newchunk;
            Dictionary<Address, string>.Enumerator iter2 = addrtosymbol.GetEnumerator();
            Dictionary<Address, string> newsymbol = new Dictionary<Address, string>();
            while (iter2.MoveNext()) {
                AddrSpace spc = iter2.Current.Key.getSpace();
                uint off = (uint)AddrSpace.addressToByte(adjust, spc.getWordSize());
                Address newaddr = iter2.Current.Key + (int)off;
                newsymbol[newaddr] = iter2.Current.Value;
            }
            addrtosymbol = newsymbol;
        }
    }
}
