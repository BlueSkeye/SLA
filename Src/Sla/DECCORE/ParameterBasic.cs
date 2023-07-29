using ghidra;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ghidra.ParameterPieces;

namespace Sla.DECCORE
{
    /// \brief A stand-alone parameter with no backing symbol
    ///
    /// Name, data-type, and storage location is stored internally to the object.
    /// This is suitable for return values, function pointer prototypes, or functions
    /// that haven't been fully analyzed.
    internal class ParameterBasic : ProtoParameter
    {
        /// The name of the parameter, "" for undefined or return value parameters
        private string name;
        /// Storage address of the parameter
        private Address addr;
        /// Data-type of the parameter
        private Datatype type;
        /// Lock and other properties from ParameterPieces flags
        private uint flags;

        /// Construct from components
        public ParameterBasic(string nm, Address ad, Datatype tp, uint fl)
        {
            name = nm; addr = ad; type = tp; flags = fl;
        }

        ///< Construct a \e void parameter
        public ParameterBasic(Datatype tp)
        {
            type = tp;
            flags = 0;
        } 

        public override string getName() => name;

        public override Datatype getType() => type;

        public override Address getAddress() => addr;

        public override int getSize() => type.getSize();

        public override bool isTypeLocked() => ((flags&ParameterPieces::typelock)!= 0);

        public override bool isNameLocked() => ((flags&ParameterPieces::namelock)!= 0);

        public override bool isSizeTypeLocked() => ((flags&ParameterPieces::sizelock)!= 0);

        public override bool isThisPointer() => ((flags&ParameterPieces::isthis)!= 0);

        public override bool isIndirectStorage() => ((flags&ParameterPieces::indirectstorage)!= 0);

        public override bool isHiddenReturn() => ((flags&ParameterPieces::hiddenretparm)!= 0);

        public override bool isNameUndefined() => (name.size()== 0);

        public override void setTypeLock(bool val)
        {
            if (val)
            {
                flags |= ParameterPieces::typelock;
                if (type.getMetatype() == TYPE_UNKNOWN) // Check if we are locking TYPE_UNKNOWN
                    flags |= ParameterPieces::sizelock;
            }
            else
                flags &= ~((uint)(ParameterPieces::typelock | ParameterPieces::sizelock));
        }

        public override void setNameLock(bool val)
        {
            if (val)
                flags |= ParameterPieces::namelock;
            else
                flags &= ~((uint)ParameterPieces::namelock);
        }

        public override void setThisPointer(bool val)
        {
            if (val)
                flags |= ParameterPieces::isthis;
            else
                flags &= ~((uint)ParameterPieces::isthis);
        }

        public override void overrideSizeLockType(Datatype ct)
        {
            if (type.getSize() == ct.getSize())
            {
                if (!isSizeTypeLocked())
                    throw new LowlevelError("Overriding parameter that is not size locked");
                type = ct;
                return;
            }
            throw new LowlevelError("Overriding parameter with different type size");
        }

        public override void resetSizeLockType(TypeFactory factory)
        {
            if (type.getMetatype() == TYPE_UNKNOWN) return; // Nothing to do
            int size = type.getSize();
            type = factory.getBase(size, TYPE_UNKNOWN);
        }

        public override ProtoParameter clone()
        {
            return new ParameterBasic(name, addr, type, flags);
        }

        public override Symbol getSymbol() 
        {
            throw new LowlevelError("Parameter is not a real symbol");
        }
    }
}
