using Sla.CORE;

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
        private uint size;

        /// Construct given a parent JumpTable
        public JumpModelTrivial(JumpTable jt)
            : base(jt)
        {
            size = 0;
        }

        public override bool isOverride() => false;

        public override int getTableSize() => (int)size;

        public override bool recoverModel(Funcdata fd, PcodeOp indop, uint matchsize,
            uint maxtablesize)
        {
            size = (uint)indop.getParent().sizeOut();
            return ((size != 0) && (size <= matchsize));
        }

        public override void buildAddresses(Funcdata fd, PcodeOp indop, List<Address> addresstable,
            List<LoadTable> loadpoints)
        {
            addresstable.Clear();
            BlockBasic bl = indop.getParent();
            for (int i = 0; i < bl.sizeOut(); ++i) {
                BlockBasic outbl = (BlockBasic)bl.getOut(i);
                addresstable.Add(outbl.getStart());
            }
        }

        public override void findUnnormalized(uint maxaddsub, uint maxleftright, uint maxext)
        {
        }

        public override void buildLabels(Funcdata fd, List<Address> addresstable,
            List<ulong> label, JumpModel orig)
        {
            for (int i = 0; i < addresstable.size(); ++i)
                // Address itself is the label
                label.Add(addresstable[i].getOffset());
        }

        public override Varnode? foldInNormalization(Funcdata fd, PcodeOp indop) => null;

        public override bool foldInGuards(Funcdata fd, JumpTable jump) => false;

        public override bool sanityCheck(Funcdata fd, PcodeOp indop, List<Address> addresstable)
            => true;

        public override JumpModel clone(JumpTable jt)
            => new JumpModelTrivial(jt) { size = size };
    }
}
