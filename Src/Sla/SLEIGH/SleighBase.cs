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
        public const uint4 MAX_UNIQUE_SIZE = 128;
        /// Current version of the .sla file read/written by SleighBash
        private const int4 SLA_FORMAT_VERSION = 3;
        
        private List<string> userop;      ///< Names of user-define p-code ops for \b this Translate object
        private Dictionary<VarnodeData, string> varnode_xref;  ///< A map from Varnodes in the \e register space to register names
        
        protected SubtableSymbol root;     ///< The root SLEIGH decoding symbol
        protected SymbolTable symtab;     ///< The SLEIGH symbol table
        protected uint4 maxdelayslotbytes;    ///< Maximum number of bytes in a delay-slot directive
        protected uint4 unique_allocatemask;  ///< Bits that are guaranteed to be zero in the unique allocation scheme
        protected uint4 numSections;      ///< Number of \e named sections
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

            for (iter = glb->begin(); iter != glb->end(); ++iter)
            {
                sym = *iter;
                if (sym->getType() == SleighSymbol::varnode_symbol)
                {
                    pair<VarnodeData, string> ins(((VarnodeSymbol*) sym)->getFixedVarnode(), sym->getName());
                    pair<map<VarnodeData, string>::iterator, bool> res = varnode_xref.insert(ins);
                    if (!res.second)
                    {
                        errorPairs.push_back(sym->getName());
                        errorPairs.push_back((*(res.first)).second);
                    }
                }
                else if (sym->getType() == SleighSymbol::userop_symbol)
                {
                    int4 index = ((UserOpSymbol*)sym)->getIndex();
                    while (userop.size() <= index)
                        userop.push_back("");
                    userop[index] = sym->getName();
                }
                else if (sym->getType() == SleighSymbol::context_symbol)
                {
                    ContextSymbol* csym = (ContextSymbol*)sym;
                    ContextField* field = (ContextField*)csym->getPatternValue();
                    int4 startbit = field->getStartBit();
                    int4 endbit = field->getEndBit();
                    registerContext(csym->getName(), startbit, endbit);
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
            for (iter = glb->begin(); iter != glb->end(); ++iter)
            {
                sym = *iter;
                if (sym->getType() == SleighSymbol::context_symbol)
                {
                    ContextSymbol* csym = (ContextSymbol*)sym;
                    ContextField* field = (ContextField*)csym->getPatternValue();
                    int4 startbit = field->getStartBit();
                    int4 endbit = field->getEndBit();
                    registerContext(csym->getName(), startbit, endbit);
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
            int4 version = 0;
            setBigEndian(xml_readbool(el->getAttributeValue("bigendian")));
            {
                istringstream s(el->getAttributeValue("align"));
                s.unsetf(ios::dec | ios::hex | ios::oct);
                s >> alignment;
            }
            {
                istringstream s(el->getAttributeValue("uniqbase"));
                s.unsetf(ios::dec | ios::hex | ios::oct);
                uintm ubase;
                s >> ubase;
                setUniqueBase(ubase);
            }
            int4 numattr = el->getNumAttributes();
            for (int4 i = 0; i < numattr; ++i)
            {
                const string &attrname(el->getAttributeName(i));
                if (attrname == "maxdelay")
                {
                    istringstream s1(el->getAttributeValue(i));
                    s1.unsetf(ios::dec | ios::hex | ios::oct);
                    s1 >> maxdelayslotbytes;
                }
                else if (attrname == "uniqmask")
                {
                    istringstream s2(el->getAttributeValue(i));
                    s2.unsetf(ios::dec | ios::hex | ios::oct);
                    s2 >> unique_allocatemask;
                }
                else if (attrname == "numsections")
                {
                    istringstream s3(el->getAttributeValue(i));
                    s3.unsetf(ios::dec | ios::hex | ios::oct);
                    s3 >> numSections;
                }
                else if (attrname == "version")
                {
                    istringstream s(el->getAttributeValue(i));
                    s.unsetf(ios::dec | ios::hex | ios::oct);
                    s >> version;
                }
            }
            if (version != SLA_FORMAT_VERSION)
                throw LowlevelError(".sla file has wrong format");
            const List &list(el->getChildren());
            List::const_iterator iter;
            iter = list.begin();
            while ((*iter)->getName() == "floatformat")
            {
                floatformats.emplace_back();
                floatformats.back().restoreXml(*iter);
                ++iter;
            }
            indexer.restoreXml(*iter);
            iter++;
            XmlDecode decoder(this,* iter);
            decodeSpaces(decoder, this);
            iter++;
            symtab.restoreXml(*iter, this);
            root = (SubtableSymbol*)symtab.getGlobalScope()->findSymbol("instruction");
            vector<string> errorPairs;
            buildXrefs(errorPairs);
            if (!errorPairs.empty())
                throw SleighError("Duplicate register pairs");
        }

        /// Construct an uninitialized translator
        public SleighBase()
        {
            root = (SubtableSymbol*)0;
            maxdelayslotbytes = 0;
            unique_allocatemask = 0;
            numSections = 0;
        }

        /// Return \b true if \b this is initialized
        public bool isInitialized() => (root != (SubtableSymbol*)0);

        ~SleighBase()
        {
        }

        public override VarnodeData getRegister(string nm)
        {
            VarnodeSymbol* sym = (VarnodeSymbol*)findSymbol(nm);
            if (sym == (VarnodeSymbol*)0)
                throw SleighError("Unknown register name: " + nm);
            if (sym->getType() != SleighSymbol::varnode_symbol)
                throw SleighError("Symbol is not a register: " + nm);
            return sym->getFixedVarnode();
        }

        public override string getRegisterName(AddrSpace @base, uintb off, int4 size)
        {
            VarnodeData sym;
            sym.space = base;
            sym.offset = off;
            sym.size = size;
            map<VarnodeData, string>::const_iterator iter = varnode_xref.upper_bound(sym); // First point greater than offset
            if (iter == varnode_xref.begin()) return "";
            iter--;
            const VarnodeData &point((*iter).first);
            if (point.space != base) return "";
            uintb offbase = point.offset;
            if (point.offset + point.size >= off + size)
                return (*iter).second;

            while (iter != varnode_xref.begin())
            {
                --iter;
                const VarnodeData &point((*iter).first);
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
        public SleighSymbol findSymbol(uintm id) => symtab.findSymbol(id);

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
            a_v(s, "defaultspace", getDefaultCodeSpace()->getName());
            s << ">\n";
            for (int4 i = 0; i < numSpaces(); ++i)
            {
                AddrSpace* spc = getSpace(i);
                if (spc == (AddrSpace*)0) continue;
                if ((spc->getType() == IPTR_CONSTANT) ||
                (spc->getType() == IPTR_FSPEC) ||
                (spc->getType() == IPTR_IOP) ||
                (spc->getType() == IPTR_JOIN))
                    continue;
                spc->saveXml(s);
            }
            s << "</spaces>\n";
            symtab.saveXml(s);
            s << "</sleigh>\n";
        }
    }
}
