using Sla.CORE;
using Sla.DECCORE;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    internal class IfcMapunionfacet : IfaceDecompCommand
    {
        /// \class IfcMapunionfacet
        /// \brief Create a union field forcing directive: `map facet <union> <fieldnum> <address> <hash>`
        ///
        /// Creates a \e facet directive that associates a given field of a \e union data-type with
        /// a varnode in the context of a specific p-code op accessing it. The varnode and p-code op
        /// are specified by dynamic hash.
        public override void execute(TextReader s)
        {
            Datatype* ct;
            string unionName;
            int fieldNum;
            int size;
            ulong hash;

            if (dcp.fd == (Funcdata*)0)
                throw IfaceExecutionError("No function loaded");
            s >> ws >> unionName;
            ct = dcp.conf.types.findByName(unionName);
            if (ct == (Datatype*)0 || ct.getMetatype() != TYPE_UNION)
                throw IfaceParseError("Bad union data-type: " + unionName);
            s >> ws >> dec >> fieldNum;
            if (fieldNum < -1 || fieldNum >= ct.numDepend())
                throw IfaceParseError("Bad field index");
            Address addr = parse_machaddr(s, size, *dcp.conf.types); // Read pc address of hash

            s >> hex >> hash;       // Parse the hash value
            ostringstream s2;
            s2 << "unionfacet" << dec << (fieldNum + 1) << '_' << hex << addr.getOffset();
            Symbol* sym = dcp.fd.getScopeLocal().addUnionFacetSymbol(s2.str(), ct, fieldNum, addr, hash);
            dcp.fd.getScopeLocal().setAttribute(sym, Varnode::typelock | Varnode::namelock);
        }
    }
}
