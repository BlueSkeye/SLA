using ghidra;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief A trivial jump-table model, where the BRANCHIND input Varnode is the switch variable
    /// This class treats the input Varnode to the BRANCHIND as the switch variable, and recovers
    /// its possible values from the existing block structure. This is used when the flow following
    /// fork recovers destination addresses, but the switch normalization action is unable to recover
    /// the model.
    internal class JumpModelTrivial : JumpModel
    {
        /// Number of addresses in the table as reported by the JumpTable
        private uint4 size;

        /// Construct given a parent JumpTable
        public JumpModelTrivial(JumpTable jt)
            : base(jt)
        {
            size = 0;
        }

        public override bool isOverride() => false;

        public override int4 getTableSize() => size;

        public override bool recoverModel(Funcdata fd, PcodeOp indop, uint4 matchsize,
            uint4 maxtablesize)
        {
            size = indop->getParent()->sizeOut();
            return ((size != 0) && (size <= matchsize));
        }

        public override void buildAddresses(Funcdata fd, PcodeOp indop, List<Address> addresstable,
            List<LoadTable> loadpoints)
        {
            addresstable.clear();
            BlockBasic* bl = indop->getParent();
            for (int4 i = 0; i < bl->sizeOut(); ++i)
            {
                BlockBasic outbl = (BlockBasic)bl->getOut(i);
                addresstable.push_back(outbl->getStart());
            }
        }

        public override void findUnnormalized(uint4 maxaddsub, uint4 maxleftright, uint4 maxext)
        {
        }

        public override void buildLabels(Funcdata fd, List<Address> addresstable,
            List<uintb> label, JumpModel orig)
        {
            for (int4 i = 0; i < addresstable.size(); ++i)
                label.push_back(addresstable[i].getOffset()); // Address itself is the label
        }

        public override Varnode foldInNormalization(Funcdata fd, PcodeOp indop) => null;

        public override bool foldInGuards(Funcdata fd, JumpTable jump) => false;

        public override bool sanityCheck(Funcdata fd, PcodeOp indop, List<Address> addresstable)
            => true;

        public override JumpModel clone(JumpTable jt)
            => new JumpModelTrivial(jt) { size = size };
    }
}
