﻿using Sla.CORE;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Sla.DECCORE
{
    internal class RuleDoubleStore : Rule
    {
        public RuleDoubleStore(string g)
            : base(g, 0, "doublestore")
        {
        }

        public override Rule? clone(ActionGroupList grouplist)
        {
            return (!grouplist.contains(getGroup())) ? null : new RuleDoubleStore(getGroup());
        }

        public override void getOpList(List<OpCode> oplist)
        {
            oplist.Add(OpCode.CPUI_STORE);
        }

        public override int applyOp(PcodeOp op, Funcdata data)
        {
            PcodeOp? storelo;
            PcodeOp? storehi;
            AddrSpace spc;

            Varnode vnlo = op.getIn(2);
            if (!vnlo.isPrecisLo()) return 0;
            if (!vnlo.isWritten()) return 0;
            PcodeOp subpieceOpLo = vnlo.getDef();
            if (subpieceOpLo.code() != OpCode.CPUI_SUBPIECE) return 0;
            if (subpieceOpLo.getIn(1).getOffset() != 0) return 0;
            Varnode whole = subpieceOpLo.getIn(0);
            if (whole.isFree()) return 0;
            IEnumerator<PcodeOp> iter = whole.beginDescend();
            while (iter.MoveNext()) {
                PcodeOp subpieceOpHi = iter.Current;
                if (subpieceOpHi.code() != OpCode.CPUI_SUBPIECE) continue;
                if (subpieceOpHi == subpieceOpLo) continue;
                int offset = (int)subpieceOpHi.getIn(1).getOffset();
                if (offset != vnlo.getSize()) continue;
                Varnode vnhi = subpieceOpHi.getOut();
                if (!vnhi.isPrecisHi()) continue;
                if (vnhi.getSize() != whole.getSize() - offset) continue;
                IEnumerator<PcodeOp> iter2 = vnhi.beginDescend();
                while (iter2.MoveNext()) {
                    PcodeOp storeOp2 = iter2.Current;
                    if (storeOp2.code() != OpCode.CPUI_STORE) continue;
                    if (storeOp2.getIn(2) != vnhi) continue;
                    if (SplitVarnode.testContiguousPointers(storeOp2, op, out storelo, out storehi, out spc)) {
                        List<PcodeOp> indirects = new List<PcodeOp>();
                        PcodeOp latest = RuleDoubleLoad.noWriteConflict(storelo, storehi, spc, indirects);
                        if (latest == (PcodeOp)null) continue;    // There was a conflict
                        if (!testIndirectUse(storelo, storehi, indirects)) continue;
                        // Create new STORE op that combines the two smaller STOREs
                        PcodeOp newstore = data.newOp(3, latest.getAddr());
                        Varnode spcvn = data.newVarnodeSpace(spc);
                        data.opSetOpcode(newstore, OpCode.CPUI_STORE);
                        data.opSetInput(newstore, spcvn, 0);
                        Varnode addrvn = storelo.getIn(1);
                        if (addrvn.isConstant())
                            addrvn = data.newConstant(addrvn.getSize(), addrvn.getOffset());
                        data.opSetInput(newstore, addrvn, 1);
                        data.opSetInput(newstore, whole, 2);
                        // We need to guarantee that -newstore- reads -addrvn- after
                        // it has been defined. So insert it after the latest.
                        data.opInsertAfter(newstore, latest);
                        data.opDestroy(op);     // Get rid of the original STOREs
                        data.opDestroy(storeOp2);
                        reassignIndirects(data, newstore, indirects);
                        return 1;
                    }
                }
            }
            return 0;
        }

        /// \brief Test if output Varnodes from a list of PcodeOps are used anywhere within a range of PcodeOps
        ///
        /// The range of PcodeOps bounded by given starting and ending PcodeOps.  An output Varnode is
        /// used within the range if there is a PcodeOp in the range that takes the Varnode as input.
        /// \param op1 is the given starting PcodeOp of the range
        /// \param op2 is the given ending PcodeOp of the range
        /// \param indirects is the list of PcodesOps whose output are tested
        /// \return \b true if no output in the list is used in the range
        public static bool testIndirectUse(PcodeOp op1, PcodeOp op2, List<PcodeOp> indirects)
        {
            if (op2.getSeqNum().getOrder() < op1.getSeqNum().getOrder()) {
                PcodeOp tmp = op2;
                op2 = op1;
                op1 = tmp;
            }
            for (int i = 0; i < indirects.size(); ++i) {
                Varnode outvn = indirects[i].getOut();
                int usecount = 0;
                int usebyop2 = 0;
                IEnumerator<PcodeOp> iter = outvn.beginDescend();
                while (iter.MoveNext()) {
                    PcodeOp op = iter.Current;
                    usecount += 1;
                    if (op.getParent() != op1.getParent()) continue;
                    if (op.getSeqNum().getOrder() < op1.getSeqNum().getOrder()) continue;
                    if (op.getSeqNum().getOrder() > op2.getSeqNum().getOrder()) continue;
                    // Its likely that INDIRECTs from the first STORE feed INDIRECTs for the second STORE
                    if (op.code() == OpCode.CPUI_INDIRECT && op2 == PcodeOp.getOpFromConst(op.getIn(1).getAddr()))
                    {
                        usebyop2 += 1;  // Note this pairing
                        continue;
                    }
                    return false;
                }
                // As an INDIRECT whose output Varnode feeds into later INDIRECTs must be removed, we need the following test.
                // If some uses of the output feed into later INDIRECTs, but not ALL do, then return false
                if (usebyop2 > 0 && usecount != usebyop2)
                    return false;
                if (usebyop2 > 1)
                    return false;
            }
            return true;
        }

        /// \brief Reassign INDIRECTs to a new given STORE
        ///
        /// The INDIRECTs are associated with old STOREs that are being removed.
        /// Each INDIRECT is moved from its position near the old STORE to be near the new STORE and
        /// the affect iop operand is set to point at the new STORE.
        /// \param data is the function owning the INDIRECTs
        /// \param newStore is the given new STORE PcodeOp
        /// \param indirects is the list of INDIRECT PcodeOps to reassign
        public static void reassignIndirects(Funcdata data, PcodeOp newStore,
            List<PcodeOp> indirects)
        {
            // Search for INDIRECT pairs.  The earlier is deleted.  The later gains the earlier's input.
            for (int i = 0; i < indirects.size(); ++i) {
                PcodeOp op = indirects[i];
                op.setMark();
                Varnode vn = op.getIn(0);
                if (!vn.isWritten()) continue;
                PcodeOp earlyop = vn.getDef() ?? throw new BugException();
                if (earlyop.isMark()) {
                    data.opSetInput(op, earlyop.getIn(0), 0);  // Grab the earlier op's input, replacing the use of its output
                    data.opDestroy(earlyop);
                }
            }
            for (int i = 0; i < indirects.size(); ++i) {
                PcodeOp op = indirects[i];
                op.clearMark();
                if (op.isDead()) continue;
                data.opUninsert(op);
                data.opInsertBefore(op, newStore);      // Move the INDIRECT to the new STORE
                data.opSetInput(op, data.newVarnodeIop(newStore), 1);   // Assign the INDIRECT to the new STORE
            }
        }
    }
}
