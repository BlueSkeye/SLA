using Sla.CORE;
using Sla.DECCORE;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using static System.Collections.Specialized.BitVector32;

namespace Sla.EXTRA
{
    internal class LoadImageBfd : LoadImage
    {
        private static int bfdinit = 0;        // Is the library (globally) initialized
        private string target;      // File format (supported by BFD)
        private bfd thebfd;
        private AddrSpace spaceid;     // We need to map space id to segments but since
                                       // we are currently ignoring segments anyway...
        private ulong bufoffset;        // Starting offset of byte buffer
        private uint bufsize;      // Number of bytes in the buffer
        private byte[] buffer;      // The actual buffer
        private /*mutable*/ asymbol[] symbol_table;
        private /*mutable*/ long number_of_symbols;
        private /*mutable*/ long cursymbol;
        private /*mutable*/ asection* secinfoptr;

        // Find section containing given offset
        private asection findSection(ulong offset, ulong ssize)
        { // Return section containing offset, or closest greater section
            asection* p;
            ulong start, stop;

            for (p = thebfd.sections; p != (asection*)NULL; p = p.next)
            {
                start = p.vma;
                secsize = (p.size != 0) ? p.size : p.rawsize;
                stop = start + secsize;
                if ((offset >= start) && (offset < stop))
                    return p;
            }
            asection* champ = (asection*)0;
            for (p = thebfd.sections; p != (asection*)NULL; p = p.next)
            {
                if (p.vma > offset)
                {
                    if (champ == (asection*)0)
                        champ = p;
                    else if (p.vma < champ.vma)
                        champ = p;
                }
            }
            return champ;
        }

        private void advanceToNextSymbol()
        {
            while (cursymbol < number_of_symbols)
            {
                asymbol a = symbol_table[cursymbol];
                if ((a.flags & BSF_FUNCTION) != 0)
                {
                    if (a.name != null)
                        return;
                }
                cursymbol += 1;
            }
        }

        public LoadImageBfd(string f, string t)
        {
            target = t;

            if (bfdinit == 0)
            {
                bfdinit = 1;
                bfd_init();
            }
            thebfd = (bfd*)0;
            spaceid = (AddrSpace)null;
            symbol_table = (asymbol**)0;

            bufsize = 512;      // Default buffer size
            bufoffset = ~((ulong)0);
            buffer = new byte[bufsize];
        }

        public void attachToSpace(AddrSpace id)
        {
            spaceid = id;
        }

        // Open any descriptors
        public void open()
        {
            if (thebfd != (bfd*)0) throw new LowlevelError("BFD library did not initialize");
            thebfd = bfd_openr(filename.c_str(), target.c_str());
            if (thebfd == (bfd*)0)
            {
                string errmsg = "Unable to open image file: ";
                errmsg += filename;
                throw new LowlevelError(errmsg);
            }
            if (!bfd_check_format(thebfd, bfd_object))
            {
                string errmsg = "File: ";
                errmsg += filename;
                errmsg += " : not in recognized object file format";
                throw new LowlevelError(errmsg);
            }
        }

        // Close any descriptor
        public void close()
        {
            bfd_close(thebfd);
            thebfd = (bfd*)0;
        }

        public void getImportTable(List<ImportRecord> irec)
        {
            throw new LowlevelError("Not implemented");
        }
        
        ~LoadImageBfd()
        {
            if (symbol_table != (asymbol**)0)
                delete[] symbol_table;
            if (thebfd != (bfd*)0)
                close();
            delete[] buffer;
        }

        // Load a chunk of image
        public override void loadFill(byte[] ptr, int size, Address addr)
        {
            asection* p;
            ulong secsize;
            ulong curaddr, offset;
            bfd_size_type readsize;
            int cursize;

            if (addr.getSpace() != spaceid)
                throw DataUnavailError("Trying to get loadimage bytes from space: " + addr.getSpace().getName());
            curaddr = addr.getOffset();
            if ((curaddr >= bufoffset) && (curaddr + size < bufoffset + bufsize))
            {   // Requested bytes were previously buffered
                byte* bufptr = buffer + (curaddr - bufoffset);
                memcpy(ptr, bufptr, size);
                return;
            }
            bufoffset = curaddr;        // Load buffer with bytes from new address
            offset = 0;
            cursize = bufsize;      // Read an entire buffer

            while (cursize > 0)
            {
                p = findSection(curaddr, secsize);
                if (p == (asection*)0)
                {
                    if (offset == 0)        // Initial address not mapped
                        break;
                    memset(buffer + offset, 0, cursize); // Fill out the rest of the buffer with 0
                    memcpy(ptr, buffer, size);
                    return;
                }
                if (p.vma > curaddr)
                {   // No section matches
                    if (offset == 0)        // Initial address not mapped
                        break;
                    readsize = p.vma - curaddr;
                    if (readsize > cursize)
                        readsize = cursize;
                    memset(buffer + offset, 0, readsize); // Fill in with zeroes to next section
                }
                else
                {
                    readsize = cursize;
                    if (curaddr + readsize > p.vma + secsize)  // Adjust to biggest possible read
                        readsize = (bfd_size_type)(p.vma + secsize - curaddr);
                    bfd_get_section_contents(thebfd, p, buffer + offset, (file_ptr)(curaddr - p.vma), readsize);
                }
                offset += readsize;
                cursize -= readsize;
                curaddr += readsize;
            }
            if (cursize > 0)
            {
                ostringstream errmsg;
                errmsg << "Unable to load " << dec << cursize << " bytes at " << addr.getShortcut();
                addr.printRaw(errmsg);
                throw DataUnavailError(errmsg.str());
            }
            memcpy(ptr, buffer, size);  // Copy requested bytes from the buffer
        }

        public override void openSymbols()
        {
            long storage_needed;
            cursymbol = 0;
            if (symbol_table != (asymbol**)0)
            {
                advanceToNextSymbol();
                return;
            }

            if (!(bfd_get_file_flags(thebfd) & HAS_SYMS))
            { // There are no symbols
                number_of_symbols = 0;
                return;
            }

            storage_needed = bfd_get_symtab_upper_bound(thebfd);
            if (storage_needed <= 0)
            {
                number_of_symbols = 0;
                return;
            }

            symbol_table = (asymbol**)new byte[storage_needed]; // Storage needed in bytes
            number_of_symbols = bfd_canonicalize_symtab(thebfd, symbol_table);
            if (number_of_symbols <= 0)
            {
                delete[] symbol_table;
                symbol_table = (asymbol**)0;
                number_of_symbols = 0;
                return;
            }
            advanceToNextSymbol();
            //  sort(symbol_table,symbol_table+number_of_symbols,compare_symbols);
        }

        public override void closeSymbols()
        {
            if (symbol_table != (asymbol**)0)
                delete[] symbol_table;
            symbol_table = (asymbol**)0;
            number_of_symbols = 0;
            cursymbol = 0;
        }

        public override bool getNextSymbol(LoadImageFunc record)
        { // Get record for next symbol if it exists, otherwise return false
            if (cursymbol >= number_of_symbols) return false;

            asymbol a = symbol_table[cursymbol];
            cursymbol += 1;
            advanceToNextSymbol();
            record.name = a.name;
            ulong val = bfd_asymbol_value(a);
            record.address = Address(spaceid, val);
            return true;
        }

        public override void openSectionInfo()
        {
            secinfoptr = thebfd.sections;
        }

        public override void closeSectionInfo()
        {
            secinfoptr = (asection*)0;
        }

        public override bool getNextSection(LoadImageSection sec)
        {
            if (secinfoptr == (asection*)0)
                return false;

            record.address = Address(spaceid, secinfoptr.vma);
            record.size = (secinfoptr.size != 0) ? secinfoptr.size : secinfoptr.rawsize;
            record.flags = 0;
            if ((secinfoptr.flags & SEC_ALLOC) == 0)
                record.flags |= LoadImageSection::unalloc;
            if ((secinfoptr.flags & SEC_LOAD) == 0)
                record.flags |= LoadImageSection::noload;
            if ((secinfoptr.flags & SEC_READONLY) != 0)
                record.flags |= LoadImageSection::readonly;
            if ((secinfoptr.flags & SEC_CODE) != 0)
                record.flags |= LoadImageSection::code;
            if ((secinfoptr.flags & SEC_DATA) != 0)
                record.flags |= LoadImageSection::data;
            secinfoptr = secinfoptr.next;
            return (secinfoptr != (asection*)0);
        }

        public override void getReadonly(RangeList list)
        { // List all ranges that are read only
            ulong start, stop, secsize;
            asection* p;

            for (p = thebfd.sections; p != (asection*)NULL; p = p.next)
            {
                if ((p.flags & SEC_READONLY) != 0)
                {
                    start = p.vma;
                    secsize = (p.size != 0) ? p.size : p.rawsize;
                    if (secsize == 0) continue;
                    stop = start + secsize - 1;
                    list.insertRange(spaceid, start, stop);
                }
            }
        }

        public override string getArchType()
        {
            string type;
            string targ;
            type = bfd_printable_name(thebfd);
            type += ':';
            targ = thebfd.xvec.name;
            type += targ;
            return type;
        }

        public override void adjustVma(long adjust)
        {
            asection* s;
            adjust = AddrSpace::addressToByte(adjust, spaceid.getWordSize());
            for (s = thebfd.sections; s != (asection*)NULL; s = s.next)
            {
                s.vma += adjust;
                s.lma += adjust;
            }
        }
    }
}
