using Sla.CORE;
using Sla.SLEIGH;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.SLEIGH
{
    internal class SleighSymbol
    {
        /// friend class SymbolTable;
        public enum symbol_type
        {
            space_symbol, token_symbol, userop_symbol, value_symbol, valuemap_symbol,
            name_symbol, varnode_symbol, varnodelist_symbol, operand_symbol,
            start_symbol, end_symbol, next2_symbol, subtable_symbol, macro_symbol, section_symbol,
            bitrange_symbol, context_symbol, epsilon_symbol, label_symbol,
            dummy_symbol
        }

        private string name;
        private uint id;           // Unique id across all symbols
        private uint scopeid;      // Unique id of scope this symbol is in
        
        public SleighSymbol()
        {
        }

        public SleighSymbol(string nm)
        {
            name = nm;
            id = 0;
        }
    
        ~SleighSymbol()
        {
        }

        public string getName() => name;

        public uint getId() => id;

        public virtual symbol_type getType() => dummy_symbol;

        public virtual void saveXmlHeader(TextWriter s)
        {               // Save the basic attributes of a symbol
            s << " name=\"" << name << "\"";
            s << " id=\"0x" << hex << id << "\"";
            s << " scope=\"0x" << scopeid << "\"";
        }

        public void restoreXmlHeader(Element el)
        {
            name = el.getAttributeValue("name");
            {
                istringstream s = new istringstream(el.getAttributeValue("id"));
                s.unsetf(ios::dec | ios::hex | ios::oct);
                s >> id;
            }
            {
                istringstream s = new istringstream(el.getAttributeValue("scope"));
                s.unsetf(ios::dec | ios::hex | ios::oct);
                s >> scopeid;
            }
        }

        public virtual void saveXml(TextWriter s)
        {
        }

        public virtual void restoreXml(Element el, SleighBase trans)
        {
        }
    }
}
