using ghidra;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief A description where and how data was loaded from memory
    ///
    /// This is a generic table description, giving the starting address
    /// of the table, the size of an entry, and number of entries.
    internal class LoadTable
    {
        // friend class EmulateFunction;
        private Address addr;           ///< Starting address of table
        private int size;          ///< Size of table entry
        private int num;           ///< Number of entries in table;

        // Constructor for use with decode
        public LoadTable()
        {
        }

        /// Constructor for a single entry table
        public LoadTable(Address ad, int sz)
        {
            addr = ad;
            size = sz;
            num = 1;
        }

        /// Construct a full table
        public LoadTable(Address ad, int sz, int nm)
        {
            addr = ad;
            size = sz;
            num = nm;
        }

        /// Compare \b this with another table by address
        public static bool operator <(LoadTable op2) => (addr<op2.addr);

        /// Encode a description of \b this as an \<loadtable> element
        /// \param encoder is the stream encoder
        public void encode(Encoder encoder)
        {
            encoder.openElement(ElementId.ELEM_LOADTABLE);
            encoder.writeSignedInteger(AttributeId.ATTRIB_SIZE, size);
            encoder.writeSignedInteger(AttributeId.ATTRIB_NUM, num);
            addr.encode(encoder);
            encoder.closeElement(ElementId.ELEM_LOADTABLE);
        }

        /// \param decoder is the stream decoder
        /// Decode \b this table from a \<loadtable> element
        public void decode(Decoder decoder)
        {
            uint elemId = decoder.openElement(ElementId.ELEM_LOADTABLE);
            size = decoder.readSignedInteger(AttributeId.ATTRIB_SIZE);
            num = decoder.readSignedInteger(AttributeId.ATTRIB_NUM);
            addr = Address::decode(decoder);
            decoder.closeElement(elemId);
        }

        /// Collapse a sequence of table descriptions
        /// We assume the list of LoadTable entries is sorted and perform an in-place
        /// collapse of any sequences into a single LoadTable entry.
        /// \param table is the list of entries to collapse
        public static void collapseTable(List<LoadTable> table)
        {
            if (table.empty()) return;
            List<LoadTable>::iterator iter, lastiter;
            int count = 1;
            iter = table.begin();
            lastiter = iter;
            Address nextaddr = (*iter).addr + (*iter).size * (*iter).num;
            ++iter;
            for (; iter != table.end(); ++iter)
            {
                if (((*iter).addr == nextaddr) && ((*iter).size == (*lastiter).size))
                {
                    (*lastiter).num += (*iter).num;
                    nextaddr = (*iter).addr + (*iter).size * (*iter).num;
                }
                else if ((nextaddr < (*iter).addr) || ((*iter).size != (*lastiter).size))
                {
                    // Starting a new table
                    lastiter++;
                    *lastiter = *iter;
                    nextaddr = (*iter).addr + (*iter).size * (*iter).num;
                    count += 1;
                }
            }
            table.resize(count, LoadTable(nextaddr, 0));
        }
    }
}
