using Sla.CORE;
using Sla.DECCORE;

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
            string unionName;
            int fieldNum;
            int size;
            ulong hash;

            if (dcp.fd == (Funcdata)null)
                throw new IfaceExecutionError("No function loaded");
            s.ReadSpaces();
            unionName = s.ReadString();
            Datatype? ct = dcp.conf.types.findByName(unionName);
            if (ct == (Datatype)null || ct.getMetatype() != type_metatype.TYPE_UNION)
                throw new IfaceParseError("Bad union data-type: " + unionName);
            s.ReadSpaces();
            fieldNum = int.Parse(s.ReadString());
            if (fieldNum < -1 || fieldNum >= ct.numDepend())
                throw new IfaceParseError("Bad field index");
            // Read pc address of hash
            Address addr = Grammar.parse_machaddr(s, out size, dcp.conf.types);

            // Parse the hash value
            s >> hex >> hash;
            Symbol sym = dcp.fd.getScopeLocal().addUnionFacetSymbol(
                $"unionfacet{(fieldNum + 1)}_{addr.getOffset():X}", ct, fieldNum, addr, hash);
            dcp.fd.getScopeLocal().setAttribute(sym,
                Varnode.varnode_flags.typelock | Varnode.varnode_flags.namelock);
        }
    }
}
