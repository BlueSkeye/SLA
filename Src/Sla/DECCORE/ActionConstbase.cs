using Sla.CORE;

using TrackedSet = System.Collections.Generic.List<Sla.CORE.TrackedContext>;

namespace Sla.DECCORE
{
    /// \brief Search for input Varnodes that have been officially provided constant values.
    ///
    /// This class injects p-code at the beginning of the function if there is an official \e uponentry
    /// injection specified for the prototype model or if there are \e tracked registers for which the
    /// user has provided a constant value for.
    internal class ActionConstbase : Action
    {
        /// Constructor
        public ActionConstbase(string g)
            : base(0, "constbase", g)
        {
        }

        public override Action? clone(ActionGroupList grouplist)
        {
            return (!grouplist.contains(getGroup())) ? null : new ActionConstbase(getGroup());
        }

        public override int apply(Funcdata data)
        {
            if (data.getBasicBlocks().getSize() == 0)
                // No blocks
                return 0;
            // Get start block, which is constructed to have nothing falling into it
            BlockBasic bb = (BlockBasic)data.getBasicBlocks().getBlock(0);

            int injectid = data.getFuncProto().getInjectUponEntry();
            if (injectid >= 0) {
                InjectPayload payload = data.getArch().pcodeinjectlib.getPayload(injectid);
                data.doLiveInject(payload, bb.getStart(), bb, bb.beginOp());
            }

            TrackedSet trackset = data.getArch().context.getTrackedSet(data.getAddress());

            for (int i = 0; i < trackset.size(); ++i) {
                TrackedContext ctx = trackset[i];

                Address addr = new Address(ctx.loc.space, ctx.loc.offset);
                PcodeOp op = data.newOp(1, bb.getStart());
                data.newVarnodeOut((int)ctx.loc.size, addr, op);
                Varnode vnin = data.newConstant((int)ctx.loc.size, ctx.val);
                data.opSetOpcode(op, OpCode.CPUI_COPY);
                data.opSetInput(op, vnin, 0);
                data.opInsertBegin(op, bb);
            }
            return 0;
        }
    }
}
