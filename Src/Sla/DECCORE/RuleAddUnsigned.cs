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
    internal class RuleAddUnsigned : Rule
    {
        public RuleAddUnsigned(string g)
            : base(g, 0, "addunsigned")
        {
        }

        public override Rule clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule*)0;
            return new RuleAddUnsigned(getGroup());
        }

        /// \class RuleAddUnsigned
        /// \brief Cleanup:  Convert INT_ADD of constants to INT_SUB:  `V + 0xff...  =>  V - 0x00...`
        public override void getOpList(List<uint> oplist)
        {
            oplist.push_back(CPUI_INT_ADD);
        }

        public override int applyOp(PcodeOp op, Funcdata data)
        {
            Varnode* constvn = op.getIn(1);

            if (!constvn.isConstant()) return 0;
            Datatype* dt = constvn.getTypeReadFacing(op);
            if (dt.getMetatype() != TYPE_UINT) return 0;
            if (dt.isCharPrint()) return 0;    // Only change integer forms
            if (dt.isEnumType()) return 0;
            ulong val = constvn.getOffset();
            ulong mask = calc_mask(constvn.getSize());
            int sa = constvn.getSize() * 6;   // 1/4 less than full bitsize
            ulong quarter = (mask >> sa) << sa;
            if ((val & quarter) != quarter) return 0;   // The first quarter of bits must all be 1's
            if (constvn.getSymbolEntry() != (SymbolEntry*)0)
            {
                EquateSymbol* sym = dynamic_cast<EquateSymbol*>(constvn.getSymbolEntry().getSymbol());
                if (sym != (EquateSymbol*)0)
                {
                    if (sym.isNameLocked())
                        return 0;       // Dont transform a named equate
                }
            }
            data.opSetOpcode(op, CPUI_INT_SUB);
            Varnode* cvn = data.newConstant(constvn.getSize(), (-val) & mask);
            cvn.copySymbol(constvn);
            data.opSetInput(op, cvn, 1);
            return 1;
        }
    }
}
