using Sla.DECCORE;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief A basic jump-table model with an added default address path
    ///
    /// This model expects two paths to the switch, 1 from a default value, 1 from the other values that hit the switch
    /// If A is the guarding control-flow block, C is the block setting the default value, and S the switch block itself,
    /// We expect one of the following situations:
    ///   - A . C or S  and  C . S
    ///   - A . C or D  and  C . S  D . S
    ///   - C . S and S . A   A . S or "out of loop", i.e. S is in a loop, and the guard block doubles as the loop condition
    ///
    /// This builds on the analysis performed for JumpBasic, which fails because there are too many paths
    /// to the BRANCHIND, preventing the guards from being interpreted properly.  This class expects to reuse
    /// the PathMeld calculation from JumpBasic.
    internal class JumpBasic2 : JumpBasic
    {
        /// The extra Varnode holding the default value
        private Varnode extravn;
        /// The set of paths that produce non-default addresses
        private PathMeld origPathMeld;

        /// \brief Check if the block that defines the normalized switch variable dominates the block containing the switch
        ///
        /// \return \b true if the switch block is dominated
        private bool checkNormalDominance()
        {
            if (normalvn.isInput())
                return true;
            FlowBlock* defblock = normalvn.getDef().getParent();
            FlowBlock* switchblock = pathMeld.getOp(0).getParent();
            while (switchblock != (FlowBlock*)0)
            {
                if (switchblock == defblock)
                    return true;
                switchblock = switchblock.getImmedDom();
            }
            return false;
        }

        protected virtual bool foldInOneGuard(Funcdata fd, GuardRecord guard, JumpTable jump)
        { // The are two main cases here:
          //    If we recovered a switch in a loop,
          //       the guard is also the loop condition, so we don't want to remove it.
          //
          //    If the guard is just deciding whether or not to use a default switch value,
          //       the guard will disappear anyway because the normalization foldin will make all its blocks donothings
          //
          // So we don't make any special mods, in case there are extra statements in these blocks

            // The final block in the table is the single value produced by the model2 guard
            jump.setLastAsMostCommon();    // It should be the default block
            guard.clear();      // Mark that we are folded
            return true;
        }

        public JumpBasic2(JumpTable jt)
            : base(jt)
        {
        }

        /// Pass in the prior PathMeld calculation
        public void initializeStart(PathMeld pMeld)
        {
            if (pMeld.empty())
            {
                extravn = (Varnode)null;
                return;
            }
            // Initialize at point where the JumpBasic model failed
            extravn = pMeld.getVarnode(pMeld.numCommonVarnode() - 1);
            origPathMeld.set(pMeld);
        }

        public override bool recoverModel(Funcdata fd, PcodeOp indop, uint matchsize,
            uint maxtablesize)
        { // Try to recover a jumptable using the second model
          // Basically there is a guard on the main switch variable,
          // Along one path, an intermediate value is set to a default constant.
          // Along the other path, the intermediate value results in a straight line calculation from the switch var
          // The two-pathed intermediate value comes together in a MULTIEQUAL, and there is a straightline
          // calculation to the BRANCHIND

            // We piggy back on the partial calculation from the basic model to see if we have the MULTIEQUAL
            Varnode* othervn = (Varnode)null;
            PcodeOp* copyop = (PcodeOp)null;
            ulong extravalue = 0;
            Varnode* joinvn = extravn;  // extravn should be set to as far back as model 1 could trace
            if (joinvn == (Varnode)null) return false;
            if (!joinvn.isWritten()) return false;
            PcodeOp* multiop = joinvn.getDef();
            if (multiop.code() != CPUI_MULTIEQUAL) return false;
            if (multiop.numInput() != 2) return false; // Must be exactly 2 paths
                                                        // Search for a constant along one of the paths
            int path;
            for (path = 0; path < 2; ++path)
            {
                Varnode* vn = multiop.getIn(path);
                if (!vn.isWritten()) continue;
                copyop = vn.getDef();
                if (copyop.code() != CPUI_COPY) continue;
                othervn = copyop.getIn(0);
                if (othervn.isConstant())
                {
                    extravalue = othervn.getOffset();
                    break;
                }
            }
            if (path == 2) return false;
            BlockBasic* rootbl = (BlockBasic*)multiop.getParent().getIn(1 - path);
            int pathout = multiop.getParent().getInRevIndex(1 - path);
            JumpValuesRangeDefault* jdef = new JumpValuesRangeDefault();
            jrange = jdef;
            jdef.setExtraValue(extravalue);
            jdef.setDefaultVn(joinvn); // Emulate the default calculation from the join point
            jdef.setDefaultOp(origPathMeld.getOp(origPathMeld.numOps() - 1));

            findDeterminingVarnodes(multiop, 1 - path);
            findNormalized(fd, rootbl, pathout, matchsize, maxtablesize);
            if (jrange.getSize() > maxtablesize)
                return false;       // We didn't find a good range

            // Insert the final sequence of operations, after the MULTIEQUAL, for constructing the address
            pathMeld.append(origPathMeld);
            varnodeIndex += origPathMeld.numCommonVarnode();    // index is pushed up by the append
            return true;
        }

        public override void findUnnormalized(uint maxaddsub, uint maxleftright, uint maxext)
        {
            normalvn = pathMeld.getVarnode(varnodeIndex);   // Normalized switch variable
            if (checkNormalDominance())
            {   // If the normal switch variable dominates the switch itself
                JumpBasic::findUnnormalized(maxaddsub, maxleftright, maxext);   // We can use the basic form of calculating the unnormalized
                return;
            }

            // We have the unusual situation that we must go BACKWARD from the unnormalized variable
            // to get to the normalized variable
            switchvn = extravn;
            PcodeOp* multiop = extravn.getDef();   // Already tested that this is a MULTIEQUAL with 2 inputs
            if ((multiop.getIn(0) == normalvn) || (multiop.getIn(1) == normalvn))
            {
                normalvn = switchvn;    // No value difference between normalized and unnormalized
            }
            else
                throw new LowlevelError("Backward normalization not implemented");
        }

        public override JumpModel clone(JumpTable jt)
        {
            JumpBasic2* res = new JumpBasic2(jt);
            res.jrange = (JumpValuesRange*)jrange.clone();    // We only need to clone the JumpValues
            return res;
        }

        public override void clear()
        {
            extravn = (Varnode)null;
            origPathMeld.clear();
            JumpBasic::clear();
        }
    }
}
