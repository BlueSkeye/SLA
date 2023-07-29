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
    internal class RuleHumptyDumpty : Rule
    {
        public RuleHumptyDumpty(string g)
            : base(g, 0, "humptydumpty")
        {
        }

        public override Rule clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule*)0;
            return new RuleHumptyDumpty(getGroup());
        }

        /// \class RuleHumptyDumpty
        /// \brief Simplify break and rejoin:  `concat( sub(V,c), sub(V,0) )  =>  V`
        ///
        /// There is also the variation:
        ///  - `concat( sub(V,c), sub(V,d) )  => sub(V,d)`
        public override void getOpList(List<uint> oplist)
        {
            oplist.push_back(CPUI_PIECE);
        }

        public override int applyOp(PcodeOp op, Funcdata data)
        {
            ulong pos1, pos2;
            int size1, size2;
            Varnode* vn1,*vn2,*root;
            PcodeOp* sub1,*sub2;
            // op is something "put together"
            vn1 = op.getIn(0);
            if (!vn1.isWritten()) return 0;
            sub1 = vn1.getDef();
            if (sub1.code() != CPUI_SUBPIECE) return 0; // from piece1
            vn2 = op.getIn(1);
            if (!vn2.isWritten()) return 0;
            sub2 = vn2.getDef();
            if (sub2.code() != CPUI_SUBPIECE) return 0; // from piece2

            root = sub1.getIn(0);
            if (root != sub2.getIn(0)) return 0; // pieces of the same whole

            pos1 = sub1.getIn(1).getOffset();
            pos2 = sub2.getIn(1).getOffset();
            size1 = vn1.getSize();
            size2 = vn2.getSize();

            if (pos1 != pos2 + size2) return 0; // Pieces do not match up

            if ((pos2 == 0) && (size1 + size2 == root.getSize()))
            {   // Pieced together whole thing
                data.opRemoveInput(op, 1);
                data.opSetInput(op, root, 0);
                data.opSetOpcode(op, CPUI_COPY);
            }
            else
            {           // Pieced together a larger part of the whole
                data.opSetInput(op, root, 0);
                data.opSetInput(op, data.newConstant(sub2.getIn(1).getSize(), pos2), 1);
                data.opSetOpcode(op, CPUI_SUBPIECE);
            }
            return 1;
        }
    }
}
