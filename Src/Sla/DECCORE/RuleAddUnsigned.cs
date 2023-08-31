using Sla.CORE;

namespace Sla.DECCORE
{
    internal class RuleAddUnsigned : Rule
    {
        public RuleAddUnsigned(string g)
            : base(g, 0, "addunsigned")
        {
        }

        public override Rule? clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule)null;
            return new RuleAddUnsigned(getGroup());
        }

        /// \class RuleAddUnsigned
        /// \brief Cleanup:  Convert INT_ADD of constants to INT_SUB:  `V + 0xff...  =>  V - 0x00...`
        public override void getOpList(List<OpCode> oplist)
        {
            oplist.Add(OpCode.CPUI_INT_ADD);
        }

        public override int applyOp(PcodeOp op, Funcdata data)
        {
            Varnode constvn = op.getIn(1) ?? throw new ApplicationException();

            if (!constvn.isConstant()) return 0;
            Datatype dt = constvn.getTypeReadFacing(op);
            if (dt.getMetatype() != type_metatype.TYPE_UINT)
                return 0;
            if (dt.isCharPrint())
                // Only change integer forms
                return 0;
            if (dt.isEnumType())
                return 0;
            ulong val = constvn.getOffset();
            ulong mask = Globals.calc_mask((uint)constvn.getSize());
            // 1/4 less than full bitsize
            int sa = constvn.getSize() * 6;
            ulong quarter = (mask >> sa) << sa;
            if ((val & quarter) != quarter)
                // The first quarter of bits must all be 1's
                return 0;
            if (constvn.getSymbolEntry() != (SymbolEntry)null) {
                EquateSymbol? sym = (constvn.getSymbolEntry().getSymbol()) as EquateSymbol;
                if (sym != (EquateSymbol)null) {
                    if (sym.isNameLocked())
                        // Dont transform a named equate
                        return 0;
                }
            }
            data.opSetOpcode(op, OpCode.CPUI_INT_SUB);
            Varnode cvn = data.newConstant(constvn.getSize(), (-val) & mask);
            cvn.copySymbol(constvn);
            data.opSetInput(op, cvn, 1);
            return 1;
        }
    }
}
