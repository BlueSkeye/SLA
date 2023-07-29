using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    internal class OptionProtoEval : ArchOption
    {
        public OptionProtoEval()
        {
            name = "protoeval";
        }

        /// \class OptionProtoEval
        /// \brief Set the prototype model to use when evaluating the parameters of the \e current function
        ///
        /// The first parameter gives the name of the prototype model. The string "default" can be given
        /// to refer to the format \e default model for the architecture. The specified model is used to
        /// evaluate parameters of the function actively being decompiled, which may be distinct from the
        /// model used to evaluate sub-functions.
        public override string apply(Architecture glb, string p1, string p2, string p3)
        {
            ProtoModel* model = (ProtoModel*)0;

            if (p1.size() == 0)
                throw ParseError("Must specify prototype model");

            if (p1 == "default")
                model = glb->defaultfp;
            else
            {
                model = glb->getModel(p1);
                if (model == (ProtoModel*)0)
                    throw ParseError("Unknown prototype model: " + p1);
            }
            string res = "Set current evaluation to " + p1;
            glb->evalfp_current = model;
            return res;
        }
    }
}
