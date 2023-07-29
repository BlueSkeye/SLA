using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Sla.DECCORE
{
    /// \brief Analyze change to the stack pointer across sub-function calls.
    internal class ActionStackPtrFlow : Action
    {
        /// Stack space associated with stack-pointer register
        private AddrSpace stackspace;
        /// True if analysis already performed
        private bool analysis_finished;

        /// \brief Calculate stack-pointer change across \e undetermined sub-functions
        /// If there are sub-functions for which \e extra \e pop is not explicit,
        /// do full linear analysis to (attempt to) recover the values.
        /// \param data is the function to analyze
        /// \param stackspace is the space associated with the stack-pointer
        /// \param spcbase is the index (relative to the stackspace) of the stack-pointer
        private static void analyzeExtraPop(Funcdata data, AddrSpace stackspace, int spcbase)
        {
            ProtoModel myfp = data.getArch().evalfp_called;
            if (myfp == null) {
                myfp = data.getArch().defaultfp;
            }
            if (myfp.getExtraPop() != ProtoModel::extrapop_unknown) {
                return;
            }

            StackSolver solver;
            try {
                solver.build(data, stackspace, spcbase);
            }
            catch (LowlevelError err) {
                data.warningHeader($"Stack frame is not setup normally: {err.explain}");
                return;
            }
            if (solver.getNumVariables() == 0) {
                return;
            }
            // Solve the equations
            solver.solve();

            Varnode invn = solver.getVariable(0);
            bool warningprinted = false;

            for (int i = 1; i < solver.getNumVariables(); ++i) {
                Varnode vn = solver.getVariable(i);
                int soln = solver.getSolution(i);
                if (soln == 65535) {
                    if (!warningprinted) {
                        data.warningHeader(
                            $"Unable to track spacebase fully for {stackspace.getName()}");
                        warningprinted = true;
                    }
                    continue;
                }
                PcodeOp op = vn.getDef();

                if (op.code() == CPUI_INDIRECT) {
                    Varnode iopvn = op.getIn(1);
                    if (iopvn.getSpace().getType() == IPTR_IOP) {
                        PcodeOp iop = PcodeOp::getOpFromConst(iopvn.getAddr());
                        FuncCallSpecs fc = data.getCallSpecs(iop);
                        if (fc != null) {
                            int soln2 = 0;
                            int comp = solver.getCompanion(i);
                            if (comp >= 0) {
                                soln2 = solver.getSolution(comp);
                            }
                            fc.setEffectiveExtraPop(soln - soln2);
                        }
                    }
                }
                List<Varnode> paramlist = new List<Varnode>();
                paramlist.Add(invn);
                int sz = invn.getSize();
                paramlist.Add(data.newConstant(sz, soln & Globals.calc_mask(sz)));
                data.opSetOpcode(op, CPUI_INT_ADD);
                data.opSetAllInput(op, paramlist);
            }
            return;
        }

        /// \brief Is the given Varnode defined as a pointer relative to the stack-pointer?
        /// Return true if -vn- is defined as the stackpointer input plus a constant (or zero)
        /// This works through the general case and the special case when the constant is zero.
        /// The constant value is passed-back to the caller.
        /// \param spcbasein is the Varnode holding the \e input value of the stack-pointer
        /// \param vn is the Varnode to check for relativeness
        /// \param constval is a reference for passing back the constant offset
        /// \return true if \b vn is stack relative
        private static bool isStackRelative(Varnode spcbasein, Varnode vn, ulong constval)
        {
            if (spcbasein == vn) {
                constval = 0;
                return true;
            }
            if (!vn.isWritten()) {
                return false;
            }
            PcodeOp addop = vn.getDef();
            if (addop.code() != CPUI_INT_ADD) {
                return false;
            }
            if (addop.getIn(0) != spcbasein) {
                return false;
            }
            Varnode constvn = addop.getIn(1);
            if (!constvn.isConstant()) {
                return false;
            }
            constval = constvn.getOffset();
            return true;
        }

        /// \brief Adjust the LOAD where the stack-pointer alias has been recovered.
        /// We've matched a LOAD with its matching store, now convert the LOAD op to a COPY of what was stored.
        /// \param data is the function being analyzed
        /// \param loadop is the LOAD op to adjust
        /// \param storeop is the matching STORE op
        /// \return true if the adjustment is successful
        private static bool adjustLoad(Funcdata data, PcodeOp loadop, PcodeOp storeop)
        {
            Varnode vn = storeop.getIn(2);
            if (vn.isConstant()) {
                vn = data.newConstant(vn.getSize(), vn.getOffset());
            }
            else if (vn.isFree()){
                return false;
            }
            data.opRemoveInput(loadop, 1);
            data.opSetOpcode(loadop, CPUI_COPY);
            data.opSetInput(loadop, vn, 0);
            return true;
        }

        /// \brief Link LOAD to matching STORE of a constant
        /// Try to find STORE op using same stack relative pointer as a given LOAD op.
        /// If we find it and the STORE stores a constant, change the LOAD to a COPY.
        /// \param data is the function owning the LOAD
        /// \param id is the stackspace
        /// \param spcbasein is the stack-pointer
        /// \param loadop is the given LOAD op
        /// \param constz is the stack relative offset of the LOAD pointer
        /// \return 1 if we successfully change LOAD to COPY, 0 otherwise
        private static int repair(Funcdata data, AddrSpace id, Varnode spcbasein, PcodeOp loadop,
            ulong constz)
        {
            int loadsize = loadop.getOut().getSize();
            BlockBasic curblock = loadop.getParent();
            list<PcodeOp*>::iterator begiter = curblock.beginOp();
            list<PcodeOp*>::iterator iter = loadop.getBasicIter();
            for (; ; ) {
                if (iter == begiter) {
                    if (curblock.sizeIn() != 1) {
                        // Can trace back to next basic block if only one path
                        return 0;
                    }
                    curblock = (BlockBasic)curblock.getIn(0);
                    begiter = curblock.beginOp();
                    iter = curblock.endOp();
                    continue;
                }
                else {
                    --iter;
                }
                PcodeOp curop = *iter;
                if (curop.isCall()) {
                    // Don't try to trace aliasing through a call
                    return 0;
                }
                if (curop.code() == CPUI_STORE) {
                    Varnode ptrvn = curop.getIn(1);
                    Varnode datavn = curop.getIn(2);
                    ulong constnew;
                    if (isStackRelative(spcbasein, ptrvn, constnew)) {
                        if ((constnew == constz) && (loadsize == datavn.getSize())) {
                            // We found the matching store
                            return (adjustLoad(data, loadop, curop)) ? 1 : 0;
                        }
                        else if ((constnew <= constz + (loadsize - 1)) && (constnew + (datavn.getSize() - 1) >= constz)) {
                            return 0;
                        }
                    }
                    else {
                        // Any other kind of STORE we can't solve aliasing
                        return 0;
                    }
                }
                else {
                    Varnode? outvn = curop.getOut();
                    if (outvn != null) {
                        if (outvn.getSpace() == id) {
                            // Stack already traced, too late
                            return 0;
                        }
                    }
                }
            }
        }

        /// \brief Find any stack pointer clogs and pass it on to the repair routines
        /// A stack pointer \b clog is a constant addition to the stack-pointer,
        /// but where the constant comes from the stack.
        /// \param data is the function to analyze
        /// \param id is the stack space
        /// \param spcbase is the index of the stack-pointer relative to the stack space
        /// \return the number of clogs that were repaired
        private static int checkClog(Funcdata data, AddrSpace id, int spcbase)
        {
            VarnodeData spacebasedata = id.getSpacebase(spcbase);
            Address spacebase = new Address(spacebasedata.space, spacebasedata.offset);
            VarnodeLocSet::const_iterator begiter, enditer;
            int clogcount = 0;

            begiter = data.beginLoc(spacebasedata.size, spacebase);
            enditer = data.endLoc(spacebasedata.size, spacebase);

            Varnode spcbasein;
            if (begiter == enditer) {
                return clogcount;
            }
            spcbasein = *begiter;
            ++begiter;
            if (!spcbasein.isInput()) {
                return clogcount;
            }
            while (begiter != enditer) {
                 outvn = *begiter;
                ++begiter;
                if (!outvn.isWritten()) {
                    continue;
                }
                PcodeOp addop = outvn.getDef();
                if (addop.code() != CPUI_INT_ADD) {
                    continue;
                }
                Varnode* y = addop.getIn(1);
                if (!y.isWritten()) {
                    // y must not be a constant
                    continue;
                }
                // is y is not constant than x (in position 0) isn't either
                Varnode x = addop.getIn(0);
                ulong constx;
                if (!isStackRelative(spcbasein, x, constx)) {
                    // If x is not stack relative
                    x = y;          // Swap x and y
                    y = addop.getIn(0);
                    if (!isStackRelative(spcbasein, x, constx)) {
                        // Now maybe the new x is stack relative
                        continue;
                    }
                }
                PcodeOp loadop = y.getDef();
                if (loadop.code() == CPUI_INT_MULT) {
                    // If we multiply
                    Varnode constvn = loadop.getIn(1);
                    if (!constvn.isConstant()) {
                        continue;
                    }
                    if (constvn.getOffset() != Globals.calc_mask(constvn.getSize())) {
                        // Must multiply by -1
                        continue;
                    }
                    y = loadop.getIn(0);
                    if (!y.isWritten()) {
                        continue;
                    }
                    loadop = y.getDef();
                }
                if (loadop.code() != CPUI_LOAD) {
                        continue;
                    }
                Varnode ptrvn = loadop.getIn(1);
                ulong constz;
                if (!isStackRelative(spcbasein, ptrvn, constz)) {
                    continue;
                }
               clogcount += repair(data, id, spcbasein, loadop, constz);
            }
            return clogcount;
        }

        ///Constructor
        public ActionStackPtrFlow(string g, AddrSpace ss)
            : base(0,"stackptrflow", g)
        {
            stackspace = ss;
        }

        public override void reset(Funcdata data)
        {
            analysis_finished = false;
        }

        public override Action? clone(ActionGroupList grouplist)
        {
            return (!grouplist.contains(getGroup()))
                ? null
                : new ActionStackPtrFlow(getGroup(), stackspace);
        }
    
        public override int apply(Funcdata data)
        {
            if (analysis_finished) {
                return 0;
            }
            if (stackspace == null) {
                // No stack to do analysis on
                analysis_finished = true;
                return 0;
            }
            int numchange = checkClog(data, stackspace, 0);
            if (numchange > 0) {
                count += 1;
            }
            if (numchange == 0) {
                analyzeExtraPop(data, stackspace, 0);
                analysis_finished = true;
            }
            return 0;
        }
    }
}
