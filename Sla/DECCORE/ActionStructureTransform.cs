using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace ghidra
{
    /// \brief Give each control-flow structure an opportunity to make a final transform
    /// This is currently used to set up \e for loops via BlockWhileDo
    internal class ActionStructureTransform : Action
    {
        /// Constructor
        public ActionStructureTransform(string g)
            : base(0,"structuretransform", g)
        {
        }

        public override Action? clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) {
                return null;
            }
            return new ActionStructureTransform(getGroup());
        }
    
        public override int apply(Funcdata data)
        {
            data.getStructure().finalTransform(data);
            return 0;
        }
    }
}
