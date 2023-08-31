using Sla.CORE;

namespace Sla.DECCORE
{
    internal class RuleIndirectCollapse : Rule
    {
        public RuleIndirectCollapse(string g)
            : base(g, 0, "indirectcollapse")
        {
        }

        public override Rule? clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule)null;
            return new RuleIndirectCollapse(getGroup());
        }

        /// \class RuleIndirectCollapse
        /// \brief Remove a OpCode.CPUI_INDIRECT if its blocking PcodeOp is dead
        public override void getOpList(List<OpCode> oplist)
        {
            oplist.Add(OpCode.CPUI_INDIRECT);
        }

        public override int applyOp(PcodeOp op, Funcdata data)
        {
            PcodeOp indop;

            if (op.getIn(1).getSpace().getType() != spacetype.IPTR_IOP) return 0;
            indop = PcodeOp.getOpFromConst(op.getIn(1).getAddr());

            // Is the indirect effect gone?
            if (!indop.isDead()) {
                if (indop.code() == OpCode.CPUI_COPY) {
                    // STORE resolved to a COPY
                    Varnode vn1 = indop.getOut();
                    Varnode vn2 = op.getOut();
                    int res = vn1.characterizeOverlap(*vn2);
                    if (res > 0) {
                        // Copy has an effect of some sort
                        if (res == 2) {
                            // vn1 and vn2 are the same storage
                            // Convert INDIRECT to COPY
                            data.opUninsert(op);
                            data.opSetInput(op, vn1, 0);
                            data.opRemoveInput(op, 1);
                            data.opSetOpcode(op, OpCode.CPUI_COPY);
                            data.opInsertAfter(op, indop);
                            return 1;
                        }
                        if (vn1.contains(vn2) == 0) {
                            // INDIRECT output is properly contained in COPY output
                            // Convert INDIRECT to a SUBPIECE
                            ulong trunc;
                            if (vn1.getSpace().isBigEndian())
                                trunc = vn1.getOffset() + vn1.getSize() - (vn2.getOffset() + vn2.getSize());
                            else
                                trunc = vn2.getOffset() - vn1.getOffset();
                            data.opUninsert(op);
                            data.opSetInput(op, vn1, 0);
                            data.opSetInput(op, data.newConstant(4, trunc), 1);
                            data.opSetOpcode(op, OpCode.CPUI_SUBPIECE);
                            data.opInsertAfter(op, indop);
                            return 1;
                        }
                        data.warning("Ignoring partial resolution of indirect", indop.getAddr());
                        return 0;       // Partial overlap, not sure what to do
                    }
                }
                else if (indop.isCall()) {
                    if (op.isIndirectCreation() || op.noIndirectCollapse())
                        return 0;
                    // If there are no aliases to a local variable, collapse
                    if (!op.getOut().hasNoLocalAlias())
                        return 0;
                }
                else if (indop.usesSpacebasePtr()) {
                    if (indop.code() == OpCode.CPUI_STORE) {
                        LoadGuard guard = data.getStoreGuard(indop);
                        if (guard != (LoadGuard)null) {
                            if (guard.isGuarded(op.getOut().getAddr()))
                                return 0;
                        }
                        else {
                            // A marked STORE that is not guarded should eventually get converted to a COPY
                            // so we keep the INDIRECT until that happens
                            return 0;
                        }
                    }
                }
                else
                    return 0;
            }

            data.totalReplace(op.getOut(), op.getIn(0));
            data.opDestroy(op);     // Get rid of the INDIRECT
            return 1;
        }
    }
}
