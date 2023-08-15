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
        
        protected SubtableSymbol? root;     ///< The root SLEIGH decoding symbol
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
            SymbolScope glb = symtab.getGlobalScope();
            SymbolTree::const_iterator iter;
            ostringstream s;

            for (iter = glb.begin(); iter != glb.end(); ++iter) {
                SleighSymbol sym = *iter;
                if (sym.getType() == SleighSymbol::varnode_symbol)
                {
                    Tuple<VarnodeData, string> ins = 
                        new Tuple<VarnodeData, string>(((VarnodeSymbol) sym).getFixedVarnode(), sym.getName());
                    pair<Dictionary<VarnodeData, string>::iterator, bool> res = varnode_xref.insert(ins);
                    if (!res.second) {
                        errorPairs.Add(sym.getName());
                        errorPairs.Add((*(res.first)).second);
                    }
                }
                else if (sym.getType() == SleighSymbol.symbol_type.userop_symbol) {
                    int index = (int)((UserOpSymbol)sym).getIndex();
                    while (userop.size() <= index)
                        userop.Add("");
                    userop[index] = sym.getName();
                }
                else if (sym.getType() == SleighSymbol.symbol_type.context_symbol) {
                    ContextSymbol csym = (ContextSymbol)sym;
                    ContextField field = (ContextField)csym.getPatternValue();
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
            SymbolScope glb = symtab.getGlobalScope();
            SymbolTree::const_iterator iter;
            SleighSymbol sym;
            for (iter = glb.begin(); iter != glb.end(); ++iter) {
                sym = *iter;
                if (sym.getType() == SleighSymbol.symbol_type.context_symbol) {
                    ContextSymbol csym = (ContextSymbol)sym;
                    ContextField field = (ContextField)csym.getPatternValue();
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
            setBigEndian(Xml.xml_readbool(el.getAttributeValue("bigendian")));
            alignment = int.Parse(el.getAttributeValue("align"));
            uint ubase = uint.Parse(el.getAttributeValue("uniqbase"));
            setUniqueBase(ubase);
            int numattr = el.getNumAttributes();
            for (int i = 0; i < numattr; ++i) {
                string attrname = el.getAttributeName(i);
                if (attrname == "maxdelay")
                    maxdelayslotbytes = uint.Parse(el.getAttributeValue(i));
                else if (attrname == "uniqmask")
                    unique_allocatemask = uint.Parse(el.getAttributeValue(i));
                else if (attrname == "numsections")
                    numSections = uint.Parse(el.getAttributeValue(i));
                else if (attrname == "version")
                    version = int.Parse(el.getAttributeValue(i));
            }
            if (version != SLA_FORMAT_VERSION)
                throw new LowlevelError(".sla file has wrong format");
            List<Element> list = el.getChildren();
            IEnumerator<Element> iter = list.GetEnumerator();
            while (iter.MoveNext()) {
                if (iter.Current.getName() != "floatformat") break;
                FloatFormat newFormat = new FloatFormat();
                newFormat.restoreXml(iter.Current);
                floatformats.Add(newFormat);
            }
            indexer.restoreXml(iter.Current);
            if (!iter.MoveNext()) throw new BugException();
            XmlDecode decoder = new XmlDecode(this, iter.Current);
            decodeSpaces(decoder, this);
            if (!iter.MoveNext()) throw new BugException();
            symtab.restoreXml(iter.Current, this);
            root = (SubtableSymbol)symtab.getGlobalScope().findSymbol("instruction");
            List<string> errorPairs = new List<string>();
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
            VarnodeSymbol sym = (VarnodeSymbol)findSymbol(nm);
            if (sym == (VarnodeSymbol)null)
                throw new SleighError("Unknown register name: " + nm);
            if (sym.getType() != SleighSymbol.symbol_type.varnode_symbol)
                throw new SleighError("Symbol is not a register: " + nm);
            return sym.getFixedVarnode();
        }

        public override string getRegisterName(AddrSpace @base, ulong off, int size)
        {
            VarnodeData sym = new VarnodeData();
            sym.space = @base;
            sym.offset = off;
            sym.size = (uint)size;
            Dictionary<VarnodeData, string>::const_iterator iter = varnode_xref.upper_bound(sym); // First point greater than offset
            if (iter == varnode_xref.begin()) return "";
            iter--;
            VarnodeData point = iter.Current.Key;
            if (point.space != @base) return "";
            ulong offbase = point.offset;
            if (point.offset + point.size >= off + size)
                return (*iter).second;

            while (iter != varnode_xref.begin()) {
                --iter;
                VarnodeData point = iter.Current.Key;
                if ((point.space != @base) || (point.offset != offbase)) return "";
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
            // Return list of all language defined user ops (with index)
            res = userop;
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
            s.Write("<sleigh");
            Xml.a_v_i(s, "version", SLA_FORMAT_VERSION);
            Xml.a_v_b(s, "bigendian", isBigEndian());
            Xml.a_v_i(s, "align", alignment);
            Xml.a_v_u(s, "uniqbase", getUniqueBase());
            if (maxdelayslotbytes > 0)
                Xml.a_v_u(s, "maxdelay", maxdelayslotbytes);
            if (unique_allocatemask != 0)
                Xml.a_v_u(s, "uniqmask", unique_allocatemask);
            if (numSections != 0)
                Xml.a_v_u(s, "numsections", numSections);
            s.WriteLine(">");
            indexer.saveXml(s);
            s.Write("<spaces");
            Xml.a_v(s, "defaultspace", getDefaultCodeSpace().getName());
            s.WriteLine(">");
            for (int i = 0; i < numSpaces(); ++i) {
                AddrSpace? spc = getSpace(i);
                if (spc == (AddrSpace)null) continue;
                if ((spc.getType() == spacetype.IPTR_CONSTANT) ||
                (spc.getType() == spacetype.IPTR_FSPEC) ||
                (spc.getType() == spacetype.IPTR_IOP) ||
                (spc.getType() == spacetype.IPTR_JOIN))
                    continue;
                spc.saveXml(s);
            }
            s.WriteLine("</spaces>");
            symtab.saveXml(s);
            s.WriteLine("</sleigh>");
        }
    }
}
