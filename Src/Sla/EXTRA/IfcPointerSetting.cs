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
    internal class IfcPointerSetting : IfaceDecompCommand
    {
        /// \class IfcPointerSetting
        /// \brief Create a pointer with additional settings: `pointer setting <name> <basetype> offset <val>`
        ///
        /// Alternately: `pointer setting <name> <basetype> space <spacename>`
        /// The new data-type is named and must be pointer.
        /// An \e offset setting creates a relative pointer and attaches the provided offset value.
        /// A \e space setting create a pointer with the provided address space as an attribute.
        void IfcPointerSetting::execute(istream &s)

        {
            if (dcp.conf == (Architecture*)0)
                throw IfaceExecutionError("No load image present");
            string typeName;
            string baseType;
            string setting;

            s >> ws;
            if (s.eof())
                throw IfaceParseError("Missing name");
            s >> typeName >> ws;
            if (s.eof())
                throw IfaceParseError("Missing base-type");
            s >> baseType >> ws;
            if (s.eof())
                throw IfaceParseError("Missing setting");
            s >> setting >> ws;
            if (setting == "offset")
            {
                int off = -1;
                s.unsetf(ios::dec | ios::hex | ios::oct); // Let user specify base
                s >> off;
                if (off <= 0)
                    throw IfaceParseError("Missing offset");
                Datatype* bt = dcp.conf.types.findByName(baseType);
                if (bt == (Datatype*)0 || bt.getMetatype() != TYPE_STRUCT)
                    throw IfaceParseError("Base-type must be a structure");
                Datatype* ptrto = TypePointerRel::getPtrToFromParent(bt, off, *dcp.conf.types);
                AddrSpace* spc = dcp.conf.getDefaultDataSpace();
                dcp.conf.types.getTypePointerRel(spc.getAddrSize(), bt, ptrto, spc.getWordSize(), off, typeName);
            }
            else if (setting == "space")
            {
                string spaceName;
                s >> spaceName;
                if (spaceName.length() == 0)
                    throw IfaceParseError("Missing name of address space");
                Datatype* ptrTo = dcp.conf.types.findByName(baseType);
                if (ptrTo == (Datatype*)0)
                    throw IfaceParseError("Unknown base data-type: " + baseType);
                AddrSpace* spc = dcp.conf.getSpaceByName(spaceName);
                if (spc == (AddrSpace*)0)
                    throw IfaceParseError("Unknown space: " + spaceName);
                dcp.conf.types.getTypePointerWithSpace(ptrTo, spc, typeName);
            }
            else
                throw IfaceParseError("Unknown pointer setting: " + setting);
            *status.optr << "Successfully created pointer: " << typeName << endl;
        }
    }
}
