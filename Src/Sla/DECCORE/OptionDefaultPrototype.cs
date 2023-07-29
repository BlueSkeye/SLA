using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    internal class OptionDefaultPrototype : ArchOption
    {
        public OptionDefaultPrototype()
        {
            name = "defaultprototype";
        }

        /// \class OptionDefaultPrototype
        /// \brief Set the default prototype model for analyzing unknown functions
        ///
        /// The first parameter must give the name of a registered prototype model.
        public override string apply(Architecture glb, string p1, string p2, string p3)
        {
            ProtoModel* model = glb->getModel(p1);
            if (model == (ProtoModel*)0)
                throw new LowlevelError("Unknown prototype model :" + p1);
            glb->setDefaultModel(model);
            return "Set default prototype to " + p1;
        }
    }
}
