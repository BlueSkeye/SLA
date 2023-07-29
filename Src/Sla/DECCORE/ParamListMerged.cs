using ghidra;
using Sla.DECCORE;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief A union of other input parameter passing models
    ///
    /// This model is viewed as a union of a constituent set of resource lists.
    /// This allows initial data-flow analysis to proceed when the exact model
    /// isn't known.  The assignMap() and fillinMap() methods are disabled for
    /// instances of this class. The controlling prototype model (ProtoModelMerged)
    /// decides from among the constituent ParamList models before these routines
    /// need to be invoked.
    internal class ParamListMerged : ParamListStandard
    {
        /// Constructor for use with decode
        public ParamListMerged()
            : base()
        {
        }

        /// Copy constructor
        public ParamListMerged(ParamListMerged op2)
            : base(op2)
        {
        }

        public void foldIn(ParamListStandard op2);				///< Add another model to the union

        public void finalize()
        {
            populateResolver();
        }               ///< Fold-ins are finished, finalize \b this

        public override uint getType() => p_merged;

        public override void assignMap(List<Datatype> proto, TypeFactory typefactory,
            List<ParameterPieces> res)
        {
            throw new LowlevelError("Cannot assign prototype before model has been resolved");
        }

        public override void fillinMap(ParamActive active)
        {
            throw new LowlevelError("Cannot determine prototype before model has been resolved");
        }

        public override ParamList clone();
    }
}
