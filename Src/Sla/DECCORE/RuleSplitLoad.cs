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
    internal class RuleSplitLoad : Rule
    {
        public RuleSplitLoad(string g)
            : base(g, 0, "splitload")
        {
        }

        public override Rule clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule*)0;
            return new RuleSplitLoad(getGroup());
        }

        /// \class RuleSplitLoad
        /// \brief Split LOAD ops based on TypePartialStruct
        ///
        /// If more than one logical component of a structure or array is loaded at once,
        /// rewrite the LOAD operator as multiple LOADs.
        public override void getOpList(List<uint> oplist)
        {
            oplist.Add(CPUI_LOAD);
        }

        public override int applyOp(PcodeOp op, Funcdata data)
        {
            Datatype* inType = SplitDatatype::getValueDatatype(op, op.getOut().getSize(), data.getArch().types);
            if (inType == (Datatype)null)
                return 0;
            type_metatype metain = inType.getMetatype();
            if (metain != type_metatype.TYPE_STRUCT && metain != type_metatype.TYPE_ARRAY && metain != type_metatype.TYPE_PARTIALSTRUCT)
                return 0;
            SplitDatatype splitter(data);
            if (splitter.splitLoad(op, inType))
                return 1;
            return 0;
        }
    }
}
