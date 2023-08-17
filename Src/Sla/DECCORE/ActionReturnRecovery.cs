using Sla.CORE;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Sla.DECCORE
{
    /// \brief Determine data-flow holding the \e return \e value of the function.
    internal class ActionReturnRecovery : Action
    {
        /// \brief Rewrite a OpCode.CPUI_RETURN op to reflect a recovered output parameter.
        ///
        /// Add a second input Varnode to the given OpCode.CPUI_RETURN PcodeOp holding the return value
        /// for the function at that point. Construct concatentations if there are multiple pieces
        /// \param active is the output parameter description
        /// \param retop is the given OpCode.CPUI_RETURN
        /// \param data is the function being analyzed
        private static void buildReturnOutput(ParamActive active, PcodeOp retop, Funcdata data)
        {
            List<Varnode*> newparam;

            newparam.Add(retop.getIn(0)); // Keep the first param (the return indirect reference)
            for (int i = 0; i < active.getNumTrials(); ++i)
            { // Gather all the used varnodes to this return in proper order
                ParamTrial & curtrial(active.getTrial(i));
                if (!curtrial.isUsed()) break;
                if (curtrial.getSlot() >= retop.numInput()) break;
                newparam.Add(retop.getIn(curtrial.getSlot()));
            }
            if (newparam.size() <= 2)   // Easy zero or one return varnode case
                data.opSetAllInput(retop, newparam);
            else if (newparam.size() == 3)
            { // Two piece concatenation case
                Varnode* lovn = newparam[1];
                Varnode* hivn = newparam[2];
                ParamTrial & triallo(active.getTrial(0));
                ParamTrial & trialhi(active.getTrial(1));
                Address joinaddr = data.getArch().constructJoinAddress(data.getArch().translate,
                                            trialhi.getAddress(), trialhi.getSize(),
                                            triallo.getAddress(), triallo.getSize());
                PcodeOp* newop = data.newOp(2, retop.getAddr());
                data.opSetOpcode(newop, OpCode.CPUI_PIECE);
                Varnode* newwhole = data.newVarnodeOut(trialhi.getSize() + triallo.getSize(), joinaddr, newop);
                newwhole.setWriteMask();       // Don't let new Varnode cause additional heritage
                data.opInsertBefore(newop, retop);
                newparam.RemoveLastItem();
                newparam.GetLastItem() = newwhole;
                data.opSetAllInput(retop, newparam);
                data.opSetInput(newop, hivn, 0);
                data.opSetInput(newop, lovn, 1);
            }
            else
            { // We may have several varnodes from a single container
              // Concatenate them into a single result
                newparam.clear();
                newparam.Add(retop.getIn(0));
                int offmatch = 0;
                Varnode* preexist = (Varnode)null;
                for (int i = 0; i < active.getNumTrials(); ++i)
                {
                    ParamTrial & curtrial(active.getTrial(i));
                    if (!curtrial.isUsed()) break;
                    if (curtrial.getSlot() >= retop.numInput()) break;
                    if (preexist == (Varnode)null)
                    {
                        preexist = retop.getIn(curtrial.getSlot());
                        offmatch = curtrial.getOffset() + curtrial.getSize();
                    }
                    else if (offmatch == curtrial.getOffset())
                    {
                        offmatch += curtrial.getSize();
                        Varnode* vn = retop.getIn(curtrial.getSlot());
                        // Concatenate the preexisting pieces with this new piece
                        PcodeOp* newop = data.newOp(2, retop.getAddr());
                        data.opSetOpcode(newop, OpCode.CPUI_PIECE);
                        Address addr = preexist.getAddr();
                        if (vn.getAddr() < addr)
                            addr = vn.getAddr();
                        Varnode* newout = data.newVarnodeOut(preexist.getSize() + vn.getSize(), addr, newop);
                        newout.setWriteMask();     // Don't let new Varnode cause additional heritage
                        data.opSetInput(newop, vn, 0);  // Most sig part
                        data.opSetInput(newop, preexist, 1);
                        data.opInsertBefore(newop, retop);
                        preexist = newout;
                    }
                    else
                        break;
                }
                if (preexist != (Varnode)null)
                    newparam.Add(preexist);
                data.opSetAllInput(retop, newparam);
            }
        }

        public ActionReturnRecovery(string g)
            : base( 0, "returnrecovery", g)
        {
        }

        public override Action? clone(ActionGroupList grouplist)
        {
            return (!grouplist.contains(getGroup())) ? null : new ActionReturnRecovery(getGroup());
        }

        public override int apply(Funcdata data)
        {
            ParamActive? active = data.getActiveOutput();
            if (active != (ParamActive)null) {
                PcodeOp op;
                Varnode vn;
                int i;

                int maxancestor = data.getArch().trim_recurse_max;
                AncestorRealistic ancestorReal;
                IEnumerator<PcodeOp> iter = data.beginOp(OpCode.CPUI_RETURN);
                while (iter.MoveNext()) {
                    op = iter.Current;
                    if (op.isDead()) continue;
                    if (op.getHaltType() != 0) continue; // Don't evaluate special halts
                    for (i = 0; i < active.getNumTrials(); ++i) {
                        ParamTrial trial = active.getTrial(i);
                        if (trial.isChecked()) continue; // Already checked
                        int slot = trial.getSlot();
                        vn = op.getIn(slot);
                        if (ancestorReal.execute(op, slot, trial, false))
                            if (data.ancestorOpUse(maxancestor, vn, op, trial, 0, 0))
                                trial.markActive(); // This varnode sees active use as a parameter
                        count += 1;
                    }
                }

                active.finishPass();
                if (active.getNumPasses() > active.getMaxPass())
                    active.markFullyChecked();

                if (active.isFullyChecked()) {
                    data.getFuncProto().deriveOutputMap(active);
                    iter = data.beginOp(OpCode.CPUI_RETURN);
                    while (iter.MoveNext()) {
                        op = iter.Current;
                        if (op.isDead()) continue;
                        if (op.getHaltType() != 0) continue;
                        buildReturnOutput(active, op, data);
                    }
                    data.clearActiveOutput();
                    count += 1;
                }
            }
            return 0;
        }
    }
}
