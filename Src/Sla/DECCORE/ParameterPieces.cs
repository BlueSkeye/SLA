﻿using Sla.CORE;

namespace Sla.DECCORE
{
    /// \brief Basic elements of a parameter: address, data-type, properties
    internal class ParameterPieces
    {
        [Flags()]
        internal enum Flags
        {
            /// Parameter is "this" pointer
            isthis = 1,
            /// Parameter is hidden pointer to return value, mirrors Varnode.varnode_flags.hiddenretparm
            hiddenretparm = 2,
            /// Parameter is indirect pointer to true parameter, mirrors Varnode.varnode_flags.indirectstorage
            indirectstorage = 4,
            /// Parameter's name is locked, mirrors Varnode.varnode_flags.namelock
            namelock = 8,
            /// Parameter's data-type is locked, mirrors Varnode.varnode_flags.typelock
            typelock = 16,
            /// Size of the parameter is locked (but not the data-type)
            sizelock = 32
        }

        /// Storage address of the parameter
        internal Address addr;
        /// The datatype of the parameter
        internal Datatype type;
        /// additional attributes of the parameter
        internal Flags flags;
    }
}
