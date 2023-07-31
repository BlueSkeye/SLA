using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief A collection parameter descriptions making up a function prototype
    ///
    /// A unified interface for accessing descriptions of individual
    /// parameters in a function prototype. Both input parameters and return values
    /// are described.
    internal abstract class ProtoStore
    {
        ~ProtoStore()
        {
        }

        /// \brief Establish name, data-type, storage of a specific input parameter
        ///
        /// This either allocates a new parameter or replaces the existing one at the
        /// specified input slot.  If there is a backing symbol table, a Symbol is
        /// created or modified.
        /// \param i is the specified input slot
        /// \param nm is the (optional) name of the parameter
        /// \param pieces holds the raw storage address and data-type to set
        /// \return the new/modified ProtoParameter
        public abstract ProtoParameter setInput(int i, string nm, ParameterPieces pieces);

        /// \brief Clear the input parameter at the specified slot
        ///
        /// The parameter is excised, any following parameters are shifted to fill its spot.
        /// If there is a backing Symbol, it is removed from the SymbolTable
        /// \param i is the specified parameter slot to remove
        public abstract void clearInput(int i);

        /// Clear all input parameters (and any backing symbols)
        public abstract void clearAllInputs();

        /// Get the number of input parameters for \b this prototype
        public abstract int getNumInputs();

        /// Get the i-th input parameter (or NULL if it doesn't exist)
        public abstract ProtoParameter getInput(int i);

        /// \brief Establish the data-type and storage of the return value
        ///
        /// This either allocates a new parameter or replaces the existing one.
        /// A \e void return value can be specified with an \e invalid address and type_metatype.TYPE_VOID data-type.
        /// \param piece holds the raw storage address and data-type to set
        /// \return the new/modified ProtoParameter
        public abstract ProtoParameter setOutput(ParameterPieces piece);

        /// Clear the return value to type_metatype.TYPE_VOID
        public abstract void clearOutput();

        /// Get the return-value description
        public abstract ProtoParameter getOutput();

        /// Clone the entire collection of parameter descriptions
        public abstract ProtoStore clone();

        /// \brief Encode any parameters that are not backed by symbols to a stream
        ///
        /// Symbols are stored elsewhere, so symbol backed parameters are not serialized.
        /// If there are any internal parameters an \<internallist> element is emitted.
        /// \param encoder is the stream encoder
        public abstract void encode(Encoder encoder);

        /// \brief Restore any internal parameter descriptions from a stream
        ///
        /// Parse an \<internallist> element containing \<param> and \<retparam> child elements.
        /// \param decoder is the stream decoder
        /// \param model is prototype model for determining storage for unassigned parameters
        public abstract void decode(Decoder decoder, ProtoModel model);
    }
}
