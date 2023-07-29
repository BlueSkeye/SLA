using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Sla.DECCORE
{
    /// \brief Set the (already) recovered output data-type as a formal part of the prototype
    internal class ActionOutputPrototype : Action
    {
        public ActionOutputPrototype(string g)
            : base(rule_onceperfunc,"outputprototype", g)
        {
        }

        public override Action? clone(ActionGroupList grouplist)
        {
            return (!grouplist.contains(getGroup())) ? null : new ActionOutputPrototype(getGroup());
        }

        public override int apply(Funcdata data)
        {
            ProtoParameter* outparam = data.getFuncProto().getOutput();
            if ((!outparam.isTypeLocked()) || outparam.isSizeTypeLocked())
            {
                PcodeOp* op = data.getFirstReturnOp();
                vector<Varnode*> vnlist;
                if (op != (PcodeOp*)0)
                {
                    for (int4 i = 1; i < op.numInput(); ++i)
                        vnlist.push_back(op.getIn(i));
                }
                if (data.isHighOn())
                    data.getFuncProto().updateOutputTypes(vnlist);
                else
                    data.getFuncProto().updateOutputNoTypes(vnlist, data.getArch().types);
            }
            return 0;
        }
    }
}
