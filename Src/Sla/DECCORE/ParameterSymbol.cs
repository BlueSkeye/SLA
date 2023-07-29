using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief A parameter with a formal backing Symbol
    ///
    /// Input parameters generally have a symbol associated with them.
    /// This class holds a reference to the Symbol object and pulls the relevant
    /// parameter information off of it.
    internal class ParameterSymbol : ProtoParameter
    {
        // friend class ProtoStoreSymbol;
        /// Backing Symbol for \b this parameter
        private Symbol sym;
        
        public ParameterSymbol()
        {
            sym = (Symbol*)0;
        }

        public override string getName() => sym.getName();

        public override Datatype getType() => sym.getType();

        public override Address getAddress() => sym.getFirstWholeMap().getAddr();

        public override int4 getSize() => sym.getFirstWholeMap().getSize();

        public override bool isTypeLocked() => sym.isTypeLocked();

        public override bool isNameLocked() => sym.isNameLocked();

        public override bool isSizeTypeLocked() => sym.isSizeTypeLocked();

        public override bool isThisPointer() => sym.isThisPointer();

        public override bool isIndirectStorage() => sym.isIndirectStorage();

        public override bool isHiddenReturn() => sym.isHiddenReturn();

        public override bool isNameUndefined() => sym.isNameUndefined();

        public override void setTypeLock(bool val)
        {
            Scope* scope = sym.getScope();
            uint4 attrs = Varnode::typelock;
            if (!sym.isNameUndefined())
                attrs |= Varnode::namelock;
            if (val)
                scope.setAttribute(sym, attrs);
            else
                scope.clearAttribute(sym, attrs);
        }

        public override void setNameLock(bool val)
        {
            Scope* scope = sym.getScope();
            if (val)
                scope.setAttribute(sym, Varnode::namelock);
            else
                scope.clearAttribute(sym, Varnode::namelock);
        }

        public override void setThisPointer(bool val)
        {
            Scope* scope = sym.getScope();
            scope.setThisPointer(sym, val);
        }

        public override void overrideSizeLockType(Datatype ct)
        {
            sym.getScope().overrideSizeLockType(sym, ct);
        }

        public override void resetSizeLockType(TypeFactory factory)
        {
            sym.getScope().resetSizeLockType(sym);
        }

        public override ProtoParameter clone()
        {
            throw new LowlevelError("Should not be cloning ParameterSymbol");
        }

        public override Symbol getSymbol() => sym;
    }
}
