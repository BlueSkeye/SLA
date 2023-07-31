using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Sla.DECCORE
{
    internal class RuleSplitCopy : Rule
    {
        public RuleSplitCopy(string g)
            : base(g, 0, "splitcopy")
        {
        }

        public override Rule clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule*)0;
            return new RuleSplitCopy(getGroup());
        }

        /// \class RuleSplitCopy
        /// \brief Split COPY ops based on TypePartialStruct
        ///
        /// If more than one logical component of a structure or array is copied at once,
        /// rewrite the COPY operator as multiple COPYs.
        public override void getOpList(List<uint> oplist)
        {
            oplist.Add(CPUI_COPY);
        }

        public override int applyOp(PcodeOp op, Funcdata data)
        {
            Datatype* inType = op.getIn(0).getTypeReadFacing(op);
            Datatype* outType = op.getOut().getTypeDefFacing();
            type_metatype metain = inType.getMetatype();
            type_metatype metaout = outType.getMetatype();
            if (metain != type_metatype.TYPE_PARTIALSTRUCT && metaout != type_metatype.TYPE_PARTIALSTRUCT &&
                metain != type_metatype.TYPE_ARRAY && metaout != type_metatype.TYPE_ARRAY &&
                metain != type_metatype.TYPE_STRUCT && metaout != type_metatype.TYPE_STRUCT)
                return false;
            SplitDatatype splitter(data);
            if (splitter.splitCopy(op, inType, outType))
                return 1;
            return 0;
        }
    }
}
