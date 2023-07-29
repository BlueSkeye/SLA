using ghidra;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief An unstructured model for passing input parameters to a function.
    ///
    /// This is the \b register model, meaning a collection of registers, any of which
    /// can be used to pass a parameter.  This is nearly identical to ParamListStandard, but
    /// rules banning \e holes are not enforced, any subset of the resource list can be used.
    /// This makes sense for executables where parameters follow no conventions or only loose
    /// conventions. The assignMap() method may make less sense in this scenario.
    internal class ParamListRegister : ParamListStandard
    {
        /// Constructor for use with decode()
        public ParamListRegister()
            : base()
        {
        }

        /// Copy constructor
        public ParamListRegister(ParamListRegister op2)
            : base(op2)
        {
        }
        
        public override uint getType() => p_register;

        public override void fillinMap(ParamActive active);

        public override ParamList clone();
}
}
