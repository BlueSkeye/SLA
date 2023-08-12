using Sla.CORE;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief A function parameter viewed as a name, data-type, and storage address
    ///
    /// This is the base class, with derived classes determining what is backing up
    /// the information, whether is it a formal Symbol or just internal storage.
    /// Both input parameters and return values can be represented with this object.
    internal abstract class ProtoParameter
    {
        public ProtoParameter()
        {
        }

        ~ProtoParameter()
        {
        }

        public abstract string getName();     ///< Get the name of the parameter ("" for return value)

        public abstract Datatype getType();       ///< Get the data-type associate with \b this

        public abstract Address getAddress();      ///< Get the storage address for \b this parameter

        public abstract int getSize();            ///< Get the number of bytes occupied by \b this parameter

        public abstract bool isTypeLocked();       ///< Is the parameter data-type locked

        public abstract bool isNameLocked();       ///< Is the parameter name locked

        public abstract bool isSizeTypeLocked();       ///< Is the size of the parameter locked

        public abstract bool isThisPointer();      ///< Is \b this the "this" pointer for a class method

        public abstract bool isIndirectStorage();      ///< Is \b this really a pointer to the true parameter

        public abstract bool isHiddenReturn();     ///< Is \b this a pointer to storage for a return value

        public abstract bool isNameUndefined();        ///< Is the name of \b this parameter undefined

        public abstract void setTypeLock(bool val);			///< Toggle the lock on the data-type

        public abstract void setNameLock(bool val);			///< Toggle the lock on the name

        public abstract void setThisPointer(bool val);      ///< Toggle whether \b this is the "this" pointer for a class method

        /// \brief Change (override) the data-type of a \e size-locked parameter.
        ///
        /// The original parameter must have a \e type-lock and type_metatype.TYPE_UNKNOWN data-type.
        /// The \e size-lock is preserved and \b this can be cleared back to its type_metatype.TYPE_UNKNOWN state.
        /// \param ct is the overriding data-type
        public abstract void overrideSizeLockType(Datatype ct);

        /// \brief Clear \b this parameter's data-type preserving any \e size-lock
        ///
        /// The data-type is converted to a type_metatype.TYPE_UNKNOWN of the same size
        /// \param factory is the TypeFactory that will construct the unknown data-type
        public abstract void resetSizeLockType(TypeFactory factory);

        public abstract ProtoParameter clone();     ///< Clone the parameter

        /// \brief Retrieve the formal Symbol associated with \b this parameter
        ///
        /// If there is no backing symbol an exception is thrown
        /// \return the backing Symbol object
        public abstract Symbol getSymbol();

        /// \brief Compare storage location and data-type for equality
        ///
        /// \param op2 is the parameter to compare with \b this
        /// \return \b true if the parameters share a data-type and storage location
        public static bool operator ==(ProtoParameter op1, ProtoParameter op2)
        {
            if (op1.getAddress() != op2.getAddress()) return false;
            if (op1.getType() != op2.getType()) return false;
            return true;
        }

        /// \brief Compare storage location and data-type for inequality
        /// \param op2 is the parameter to compare with \b this
        /// \return \b true if the parameters do not share a data-type and storage location
        public static bool operator !=(ProtoParameter op1, ProtoParameter op2)
        {
            return !(op1 == op2);
        }
    }
}
