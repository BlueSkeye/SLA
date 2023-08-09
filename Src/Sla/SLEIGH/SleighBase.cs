using Sla.CORE;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.SymbolStore;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Text;
using System.Threading.Tasks;

namespace Sla.SLEIGH
{
    /// \brief Common core of classes that read or write SLEIGH specification files natively.

    ///
    /// This class represents what's in common across the SLEIGH infrastructure between:
    ///   - Reading the various SLEIGH specification files
    ///   - Building and writing out SLEIGH specification files
    internal class SleighBase : Translate
    {
        /// Maximum size of a varnode in the unique space (should match value in SleighBase.java)
        public const uint MAX_UNIQUE_SIZE = 128;
        /// Current version of the .sla file read/written by SleighBash
        private const int SLA_FORMAT_VERSION = 3;
        
        private List<string> userop;      ///< Names of user-define p-code ops for \b this Translate object
        private Dictionary<VarnodeData, string> varnode_xref;  ///< A map from Varnodes in the \e register space to register names
        
        protected SubtableSymbol root;     ///< The root SLEIGH decoding symbol
        protected SymbolTable symtab;     ///< The SLEIGH symbol table
        protected uint maxdelayslotbytes;    ///< Maximum number of bytes in a delay-slot directive
        protected uint unique_allocatemask;  ///< Bits that are guaranteed to be zero in the unique allocation scheme
        protected uint numSections;      ///< Number of \e named sections
        protected SourceFileIndexer indexer;    ///< source file index used when generating SLEIGH constructor debug info

        /// Build register map. Collect user-ops and context-fields.
        /// Assuming the symbol table is populated, iterate through the table collecting
        /// registers (for the map), user-op names, and context fields.
        protected void buildXrefs(List<string> errorPairs)
        {
            SymbolScope* glb = symtab.getGlobalScope();
            SymbolTree::const_iterator iter;
            SleighSymbol* sym;
            ostringstream s;

            for (iter = glb.begin(); iter != glb.end(); ++iter)
            {
                sym = *iter;
                if (sym.getType() == SleighSymbol::varnode_symbol)
                {
                    pair<VarnodeData, string> ins(((VarnodeSymbol*) sym).getFixedVarnode(), sym.getName());
                    pair<Dictionary<VarnodeData, string>::iterator, bool> res = varnode_xref.insert(ins);
                    if (!res.second)
                    {
                        errorPairs.Add(sym.getName());
                        errorPairs.Add((*(res.first)).second);
                    }
                }
                else if (sym.getType() == SleighSymbol::userop_symbol)
                {
                    int index = ((UserOpSymbol*)sym).getIndex();
                    while (userop.size() <= index)
                        userop.Add("");
                    userop[index] = sym.getName();
                }
                else if (sym.getType() == SleighSymbol::context_symbol)
                {
                    ContextSymbol* csym = (ContextSymbol*)sym;
                    ContextField* field = (ContextField*)csym.getPatternValue();
                    int startbit = field.getStartBit();
                    int endbit = field.getEndBit();
                    registerContext(csym.getName(), startbit, endbit);
                }
            }
        }

        /// Reregister context fields for a new executable
        /// If \b this SleighBase is being reused with a new program, the context
        /// variables need to be registered with the new program's database
        protected void reregisterContext()
        {
            SymbolScope* glb = symtab.getGlobalScope();
            SymbolTree::const_iterator iter;
            SleighSymbol* sym;
            for (iter = glb.begin(); iter != glb.end(); ++iter)
            {
                sym = *iter;
                if (sym.getType() == SleighSymbol::context_symbol)
                {
                    ContextSymbol* csym = (ContextSymbol*)sym;
                    ContextField* field = (ContextField*)csym.getPatternValue();
                    int startbit = field.getStartBit();
                    int endbit = field.getEndBit();
                    registerContext(csym.getName(), startbit, endbit);
                }
            }
        }

        /// Read a SLEIGH specification from XML
        /// This parses the main \<sleigh> tag (from a .sla file), which includes the description
        /// of address spaces and the symbol table, with its associated decoding tables
        /// \param el is the root XML element
        protected void restoreXml(Element el)
        {
            maxdelayslotbytes = 0;
            unique_allocatemask = 0;
            numSections = 0;
            int version = 0;
            setBigEndian(xml_readbool(el.getAttributeValue("bigendian")));
            {
                istringstream s = new istringstream(el.getAttributeValue("align"));
                s.unsetf(ios::dec | ios::hex | ios::oct);
                s >> alignment;
            }
            {
                istringstream s = new istringstream(el.getAttributeValue("uniqbase"));
                s.unsetf(ios::dec | ios::hex | ios::oct);
                uint ubase;
                s >> ubase;
                setUniqueBase(ubase);
            }
            int numattr = el.getNumAttributes();
            for (int i = 0; i < numattr; ++i)
            {
                string attrname = el.getAttributeName(i);
                if (attrname == "maxdelay")
                {
                    istringstream s1(el.getAttributeValue(i));
                    s1.unsetf(ios::dec | ios::hex | ios::oct);
                    s1 >> maxdelayslotbytes;
                }
                else if (attrname == "uniqmask")
                {
                    istringstream s2(el.getAttributeValue(i));
                    s2.unsetf(ios::dec | ios::hex | ios::oct);
                    s2 >> unique_allocatemask;
                }
                else if (attrname == "numsections")
                {
                    istringstream s3(el.getAttributeValue(i));
                    s3.unsetf(ios::dec | ios::hex | ios::oct);
                    s3 >> numSections;
                }
                else if (attrname == "version")
                {
                    istringstream s = new istringstream(el.getAttributeValue(i));
                    s.unsetf(ios::dec | ios::hex | ios::oct);
                    s >> version;
                }
            }
            if (version != SLA_FORMAT_VERSION)
                throw new LowlevelError(".sla file has wrong format");
            List list = el.getChildren();
            List::const_iterator iter;
            iter = list.begin();
            while ((*iter).getName() == "floatformat")
            {
                floatformats.emplace_back();
                floatformats.GetLastItem().restoreXml(*iter);
                ++iter;
            }
            indexer.restoreXml(*iter);
            iter++;
            XmlDecode decoder(this,* iter);
            decodeSpaces(decoder, this);
            iter++;
            symtab.restoreXml(*iter, this);
            root = (SubtableSymbol*)symtab.getGlobalScope().findSymbol("instruction");
            List<string> errorPairs;
            buildXrefs(errorPairs);
            if (!errorPairs.empty())
                throw new SleighError("Duplicate register pairs");
        }

        /// Construct an uninitialized translator
        public SleighBase()
        {
            root = (SubtableSymbol)null;
            maxdelayslotbytes = 0;
            unique_allocatemask = 0;
            numSections = 0;
        }

        /// Return \b true if \b this is initialized
        public bool isInitialized() => (root != (SubtableSymbol)null);

        ~SleighBase()
        {
        }

        public override VarnodeData getRegister(string nm)
        {
            VarnodeSymbol* sym = (VarnodeSymbol*)findSymbol(nm);
            if (sym == (VarnodeSymbol*)0)
                throw new SleighError("Unknown register name: " + nm);
            if (sym.getType() != SleighSymbol::varnode_symbol)
                throw new SleighError("Symbol is not a register: " + nm);
            return sym.getFixedVarnode();
        }

        public override string getRegisterName(AddrSpace @base, ulong off, int size)
        {
            VarnodeData sym;
            sym.space = base;
            sym.offset = off;
            sym.size = size;
            Dictionary<VarnodeData, string>::const_iterator iter = varnode_xref.upper_bound(sym); // First point greater than offset
            if (iter == varnode_xref.begin()) return "";
            iter--;
            VarnodeData point = iter.Current.Key;
            if (point.space != base) return "";
            ulong offbase = point.offset;
            if (point.offset + point.size >= off + size)
                return (*iter).second;

            while (iter != varnode_xref.begin())
            {
                --iter;
                VarnodeData point = iter.Current.Key;
                if ((point.space != base) || (point.offset != offbase)) return "";
                if (point.offset + point.size >= off + size)
                    return (*iter).second;
            }
            return "";
        }

        public override void getAllRegisters(Dictionary<VarnodeData, string> reglist)
        {
            reglist = varnode_xref;
        }

        public override void getUserOpNames(List<string> res)
        {
            res = userop;       // Return list of all language defined user ops (with index)
        }

        /// Find a specific SLEIGH symbol by name in the current scope
        public SleighSymbol findSymbol(string nm) => symtab.findSymbol(nm);

        /// Find a specific SLEIGH symbol by id
        public SleighSymbol findSymbol(uint id) => symtab.findSymbol(id);

        /// Find a specific global SLEIGH symbol by name
        public SleighSymbol findGlobalSymbol(string nm) => symtab.findGlobalSymbol(nm);

        /// Write out the SLEIGH specification as an XML \<sleigh> tag.
        /// This does the bulk of the work of creating a .sla file
        /// \param s is the output stream
        public void saveXml(TextWriter s)
        {
            s << "<sleigh";
            a_v_i(s, "version", SLA_FORMAT_VERSION);
            a_v_b(s, "bigendian", isBigEndian());
            a_v_i(s, "align", alignment);
            a_v_u(s, "uniqbase", getUniqueBase());
            if (maxdelayslotbytes > 0)
                a_v_u(s, "maxdelay", maxdelayslotbytes);
            if (unique_allocatemask != 0)
                a_v_u(s, "uniqmask", unique_allocatemask);
            if (numSections != 0)
                a_v_u(s, "numsections", numSections);
            s << ">\n";
            indexer.saveXml(s);
            s << "<spaces";
            a_v(s, "defaultspace", getDefaultCodeSpace().getName());
            s << ">\n";
            for (int i = 0; i < numSpaces(); ++i)
            {
                AddrSpace* spc = getSpace(i);
                if (spc == (AddrSpace)null) continue;
                if ((spc.getType() == spacetype.IPTR_CONSTANT) ||
                (spc.getType() == spacetype.IPTR_FSPEC) ||
                (spc.getType() == spacetype.IPTR_IOP) ||
                (spc.getType() == spacetype.IPTR_JOIN))
                    continue;
                spc.saveXml(s);
            }
            s << "</spaces>\n";
            symtab.saveXml(s);
            s << "</sleigh>\n";
        }
    }
}
