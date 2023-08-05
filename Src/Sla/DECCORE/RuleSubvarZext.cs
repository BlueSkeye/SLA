using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Sla.DECCORE
{
    internal class RuleSubvarZext : Rule
    {
        public RuleSubvarZext(string g)
            : base(g, 0, "subvar_zext")
        {
        }

        public override Rule clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule)null;
            return new RuleSubvarZext(getGroup());
        }

        /// \class RuleSubvarZext
        /// \brief Perform SubvariableFlow analysis triggered by INT_ZEXT
        public override void getOpList(List<OpCode> oplist)
        {
            oplist.Add(OpCode.CPUI_INT_ZEXT);
        }

        public override int applyOp(PcodeOp op, Funcdata data)
        {
            Varnode* vn = op.getOut();
            Varnode* invn = op.getIn(0);
            ulong mask = Globals.calc_mask(invn.getSize());

            SubvariableFlow subflow = new SubvariableFlow(&data,vn,mask,invn.isPtrFlow(),false,false);
            if (!subflow.doTrace()) return 0;
            subflow.doReplacement();
            return 1;
        }
    }
}
