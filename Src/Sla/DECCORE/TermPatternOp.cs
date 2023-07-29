using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Sla.DECCORE
{
    /// \brief A user defined p-code op that has a dynamically defined procedure
    ///
    /// The behavior of this op on constant inputs can be dynamically defined.
    /// This class defines a unify() method that picks out the input varnodes to the
    /// operation, given the root PcodeOp.  The input varnodes would generally just be
    /// the input varnodes to the raw CALLOTHER after the constant id, but skipping, reordering,
    /// or other tree traversal is possible.
    ///
    /// This class also defines an execute() method that computes the output given
    /// constant inputs (matching the format determined by unify()).
    internal abstract class TermPatternOp : UserPcodeOp
    {
        public TermPatternOp(Architecture g, string nm,int4 ind)
            : base(g, nm, ind)
        {
        }

        /// Get the number of input Varnodes expected
        public abstract int4 getNumVariableTerms();

        /// \brief Gather the formal input Varnode objects given the root PcodeOp
        ///
        /// \param data is the function being analyzed
        /// \param op is the root operation
        /// \param bindlist will hold the ordered list of input Varnodes
        /// \return \b true if the requisite inputs were found
        public abstract bool unify(Funcdata data, PcodeOp op, List<Varnode> bindlist);

        /// \brief Compute the output value of \b this operation, given constant inputs
        ///
        /// \param input is the ordered list of constant inputs
        /// \return the resulting value as a constant
        public abstract uintb execute(List<uintb> input);
    }
}
