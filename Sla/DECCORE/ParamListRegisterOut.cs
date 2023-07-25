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
    /// \brief A model for passing back return values from a function
    ///
    /// This is a resource list of potential storage locations for a return value,
    /// at most 1 of which will be chosen for a given function. This models a simple strategy
    /// for selecting a storage location. When assigning based on data-type (assignMap), the first list
    /// entry that fits is chosen.  When assigning from a set of actively used locations (fillinMap),
    /// this class chooses the location that is the closest fitting match to an entry in the resource list.
    internal class ParamListRegisterOut : ParamListStandard
    {
        /// Constructor
        public ParamListRegisterOut()
            : base()
        {
        }

        /// Copy constructor
        public ParamListRegisterOut(ParamListRegisterOut op2)
            : base(op2)
        {
        }
        
        public override uint4 getType() => p_register_out;

        public override void assignMap(List<Datatype> proto, TypeFactory typefactory,
            List<ParameterPieces> res);

        public override void fillinMap(ParamActive active);

        public override bool possibleParam(Address loc, int4 size);

        public override ParamList clone();
    }
}
