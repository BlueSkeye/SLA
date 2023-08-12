using Sla.CORE;
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
        internal Symbol? sym;
        
        public ParameterSymbol()
        {
            sym = (Symbol)null;
        }

        private Symbol AssertedNonNullSymbol => sym ?? throw new BugException();

        public override string getName() => AssertedNonNullSymbol.getName();

        public override Datatype getType() => AssertedNonNullSymbol.getType() ?? throw new BugException();

        public override Address getAddress() => AssertedNonNullSymbol.getFirstWholeMap().getAddr();

        public override int getSize() => AssertedNonNullSymbol.getFirstWholeMap().getSize();

        public override bool isTypeLocked() => AssertedNonNullSymbol.isTypeLocked();

        public override bool isNameLocked() => AssertedNonNullSymbol.isNameLocked();

        public override bool isSizeTypeLocked() => AssertedNonNullSymbol.isSizeTypeLocked();

        public override bool isThisPointer() => AssertedNonNullSymbol.isThisPointer();

        public override bool isIndirectStorage() => AssertedNonNullSymbol.isIndirectStorage();

        public override bool isHiddenReturn() => AssertedNonNullSymbol.isHiddenReturn();

        public override bool isNameUndefined() => AssertedNonNullSymbol.isNameUndefined();

        public override void setTypeLock(bool val)
        {
            Scope scope = AssertedNonNullSymbol.getScope();
            Varnode.varnode_flags attrs = Varnode.varnode_flags.typelock;
            if (!AssertedNonNullSymbol.isNameUndefined())
                attrs |= Varnode.varnode_flags.namelock;
            if (val)
                scope.setAttribute(AssertedNonNullSymbol, attrs);
            else
                scope.clearAttribute(AssertedNonNullSymbol, attrs);
        }

        public override void setNameLock(bool val)
        {
            Scope scope = AssertedNonNullSymbol.getScope();
            if (val)
                scope.setAttribute(AssertedNonNullSymbol, Varnode.varnode_flags.namelock);
            else
                scope.clearAttribute(AssertedNonNullSymbol, Varnode.varnode_flags.namelock);
        }

        public override void setThisPointer(bool val)
        {
            Scope scope = AssertedNonNullSymbol.getScope();
            scope.setThisPointer(AssertedNonNullSymbol, val);
        }

        public override void overrideSizeLockType(Datatype ct)
        {
            AssertedNonNullSymbol.getScope().overrideSizeLockType(AssertedNonNullSymbol, ct);
        }

        public override void resetSizeLockType(TypeFactory factory)
        {
            AssertedNonNullSymbol.getScope().resetSizeLockType(AssertedNonNullSymbol);
        }

        public override ProtoParameter clone()
        {
            throw new LowlevelError("Should not be cloning ParameterSymbol");
        }

        public override Symbol getSymbol() => AssertedNonNullSymbol;
    }
}
