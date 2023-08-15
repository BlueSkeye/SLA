using Sla.CORE;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Sla.DECCORE
{
    internal class RuleDoubleLoad : Rule
    {
        public RuleDoubleLoad(string g)
            : base(g, 0, "doubleload")
        {
        }

        public override Rule? clone(ActionGroupList grouplist)
        {
            return (!grouplist.contains(getGroup())) ? null : new RuleDoubleLoad(getGroup());
        }
        public virtual void getOpList(List<uint> oplist)
        {
            oplist.Add(OpCode.CPUI_PIECE);
        }

        public override bool applyOp(PcodeOp op, Funcdata data)
        {
            // Load from lowest address, highest (NOT significance)
            PcodeOp? loadlo;
            PcodeOp? loadhi;
            AddrSpace spc;
            int size;

            Varnode piece0 = op.getIn(0);
            Varnode piece1 = op.getIn(1);
            if (!piece0.isWritten()) return 0;
            if (!piece1.isWritten()) return 0;
            if (piece0.getDef().code() != OpCode.CPUI_LOAD) return 0;
            if (piece1.getDef().code() != OpCode.CPUI_LOAD) return 0;
            if (!SplitVarnode.testContiguousPointers(piece0.getDef(), piece1.getDef(),
                out loadlo, out loadhi, out spc))
                return 0;

            size = piece0.getSize() + piece1.getSize();
            PcodeOp latest = noWriteConflict(loadlo, loadhi, spc, (List<PcodeOp>)null);
            if (latest == (PcodeOp)null) return 0; // There was a conflict

            // Create new load op that combines the two smaller loads
            PcodeOp newload = data.newOp(2, latest.getAddr());
            Varnode vnout = data.newUniqueOut(size, newload);
            Varnode spcvn = data.newVarnodeSpace(spc);
            data.opSetOpcode(newload, OpCode.CPUI_LOAD);
            data.opSetInput(newload, spcvn, 0);
            Varnode addrvn = loadlo.getIn(1);
            if (addrvn.isConstant())
                addrvn = data.newConstant(addrvn.getSize(), addrvn.getOffset());
            data.opSetInput(newload, addrvn, 1);
            // We need to guarantee that -newload- reads -addrvn- after
            // it has been defined. So insert it after the latest.
            data.opInsertAfter(newload, latest);

            // Change the concatenation to a copy from the big load
            data.opRemoveInput(op, 1);
            data.opSetOpcode(op, OpCode.CPUI_COPY);
            data.opSetInput(op, vnout, 0);

            return 1;
        }

        /// \brief Scan for conflicts between two LOADs or STOREs that would prevent them from being combined
        ///
        /// The PcodeOps must be in the same basic block.  Each PcodeOp that falls in between is examined
        /// to determine if it writes to the same address space as the LOADs or STOREs, which indicates that
        /// combining isn't possible.  If the LOADs and STOREs can be combined, the later of the two PcodeOps
        /// is returned, otherwise null is returned.
        ///
        /// In the case of STORE ops, an extra container for INDIRECT PcodeOps is passed @in.  INDIRECTs that
        /// are caused by the STORE ops themselves are collected in the container.
        /// \param op1 is a given LOAD or STORE
        /// \param op2 is the other given LOAD or STORE
        /// \param spc is the address space referred to by the LOAD/STOREs
        /// \param indirects if non-null is used to collect INDIRECTs caused by STOREs
        public static PcodeOp? noWriteConflict(PcodeOp op1, PcodeOp op2, AddrSpace spc,
            List<PcodeOp> indirects)
        {
            BlockBasic bb = op1.getParent();

            // Force the two ops to be in the same basic block
            if (bb != op2.getParent()) return (PcodeOp)null;
            if (op2.getSeqNum().getOrder() < op1.getSeqNum().getOrder()) {
                PcodeOp tmp = op2;
                op2 = op1;
                op1 = tmp;
            }
            PcodeOp startop = op1;
            if (op1.code() == OpCode.CPUI_STORE) {
                // Extend the range of PcodeOps to include any CPUI_INDIRECTs associated with the initial STORE
                PcodeOp? tmpOp = startop.previousOp();
                while (tmpOp != (PcodeOp)null && tmpOp.code() == OpCode.CPUI_INDIRECT)
                {
                    startop = tmpOp;
                    tmpOp = tmpOp.previousOp();
                }
            }
            IEnumerator<PcodeOp> iter = startop.getBasicIter();
            IEnumerator<PcodeOp> enditer = op2.getBasicIter();

            while (iter != enditer) {
                PcodeOp curop = *iter;
                Varnode outvn;
                PcodeOp affector;
                ++iter;
                if (curop == op1) continue;
                switch (curop.code()) {
                    case OpCode.CPUI_STORE:
                        if (curop.getIn(0).getSpaceFromConst() == spc)
                            return (PcodeOp)null; // Don't go any further trying to resolve alias
                        break;
                    case OpCode.CPUI_INDIRECT:
                        affector = PcodeOp.getOpFromConst(curop.getIn(1).getAddr());
                        if (affector == op1 || affector == op2) {
                            if (indirects != (List<PcodeOp>)null)
                                indirects.Add(curop);
                        }
                        else {
                            if (curop.getOut().getSpace() == spc)
                                return (PcodeOp)null;
                        }
                        break;
                    case OpCode.CPUI_CALL:
                    case OpCode.CPUI_CALLIND:
                    case OpCode.CPUI_CALLOTHER:
                    case OpCode.CPUI_RETURN:
                    case OpCode.CPUI_BRANCH:
                    case OpCode.CPUI_CBRANCH:
                    case OpCode.CPUI_BRANCHIND:
                        return (PcodeOp)null;
                    default:
                        outvn = curop.getOut();
                        if (outvn != (Varnode)null)
                        {
                            if (outvn.getSpace() == spc)
                                return (PcodeOp)null;
                        }
                        break;
                }
            }
            return op2;
        }
    }
}
