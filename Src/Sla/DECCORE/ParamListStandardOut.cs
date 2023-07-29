using ghidra;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief A standard model for returning output parameters from a function
    ///
    /// This has a more involved assignment strategy than its parent class.
    /// Entries in the resource list are treated as a \e group, meaning that only one can
    /// fit the desired storage size and type attributes of the return value. If no entry
    /// fits, the return value is converted to a pointer data-type, storage allocation is
    /// attempted again, and the return value is marked as a \e hidden return parameter
    /// to inform the input model.
    internal class ParamListStandardOut : ParamListRegisterOut
    {
        /// Constructor for use with decode()
        public ParamListStandardOut()
            : base()
        {
        }

        /// Copy constructor
        public ParamListStandardOut(ParamListStandardOut op2)
            : base(op2)
        {
        }
        
        public override uint getType() => p_standard_out;

        public override void assignMap(List<Datatype> proto, TypeFactory typefactory,
            List<ParameterPieces> res);

        public override void decode(Decoder decoder, List<EffectRecord> effectlist,
            bool normalstack);

        public override ParamList clone();
    }
}
