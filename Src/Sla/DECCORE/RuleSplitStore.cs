﻿using System;
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
    internal class RuleSplitStore : Rule
    {
        public RuleSplitStore(string g)
            : base(g, 0, "splitstore")
        {
        }

        public override Rule clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule*)0;
            return new RuleSplitStore(getGroup());
        }

        /// \class RuleSplitStore
        /// \brief Split STORE ops based on TypePartialStruct
        ///
        /// If more than one logical component of a structure or array is stored at once,
        /// rewrite the STORE operator as multiple STOREs.
        public override void getOpList(List<uint4> oplist)
        {
            oplist.push_back(CPUI_STORE);
        }

        public override int4 applyOp(PcodeOp op, Funcdata data)
        {
            Datatype* outType = SplitDatatype::getValueDatatype(op, op.getIn(2).getSize(), data.getArch().types);
            if (outType == (Datatype*)0)
                return 0;
            type_metatype metain = outType.getMetatype();
            if (metain != TYPE_STRUCT && metain != TYPE_ARRAY && metain != TYPE_PARTIALSTRUCT)
                return 0;
            SplitDatatype splitter(data);
            if (splitter.splitStore(op, outType))
                return 1;
            return 0;
        }
    }
}
