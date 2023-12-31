﻿using Sla.CORE;
using Sla.DECCORE;

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
        public override void execute(TextReader s)
        {
            if (dcp.conf == (Architecture)null)
                throw new IfaceExecutionError("No load image present");
            string typeName;
            string baseType;
            string setting;

            if (s.ReadSpaces())
                throw new IfaceParseError("Missing name");
            typeName = s.ReadString();
            if (s.ReadSpaces())
                throw new IfaceParseError("Missing base-type");
            baseType = s.ReadString();
            if (s.ReadSpaces())
                throw new IfaceParseError("Missing setting");
            setting = s.ReadString();
            if (setting == "offset") {
                int off = -1;
                // Let user specify base
                off = (int)s.ReadDecimalUnsignedIntegerWithUnknownBase();
                if (off <= 0)
                    throw new IfaceParseError("Missing offset");
                Datatype? bt = dcp.conf.types.findByName(baseType);
                if (bt == (Datatype)null || bt.getMetatype() != type_metatype.TYPE_STRUCT)
                    throw new IfaceParseError("Base-type must be a structure");
                Datatype ptrto = TypePointerRel.getPtrToFromParent(bt, off, dcp.conf.types);
                AddrSpace spc = dcp.conf.getDefaultDataSpace();
                dcp.conf.types.getTypePointerRel((int)spc.getAddrSize(), bt, ptrto,
                    (int)spc.getWordSize(), off, typeName);
            }
            else if (setting == "space") {
                string spaceName = s.ReadString();
                if (spaceName.Length == 0)
                    throw new IfaceParseError("Missing name of address space");
                Datatype? ptrTo = dcp.conf.types.findByName(baseType);
                if (ptrTo == (Datatype)null)
                    throw new IfaceParseError("Unknown base data-type: " + baseType);
                AddrSpace? spc = dcp.conf.getSpaceByName(spaceName);
                if (spc == (AddrSpace)null)
                    throw new IfaceParseError("Unknown space: " + spaceName);
                dcp.conf.types.getTypePointerWithSpace(ptrTo, spc, typeName);
            }
            else
                throw new IfaceParseError("Unknown pointer setting: " + setting);
            status.optr.WriteLine($"Successfully created pointer: {typeName}");
        }
    }
}
