
namespace Sla.DECCORE
{
    /// \brief Set the (already) recovered output data-type as a formal part of the prototype
    internal class ActionOutputPrototype : Action
    {
        public ActionOutputPrototype(string g)
            : base(ruleflags.rule_onceperfunc,"outputprototype", g)
        {
        }

        public override Action? clone(ActionGroupList grouplist)
        {
            return (!grouplist.contains(getGroup())) ? null : new ActionOutputPrototype(getGroup());
        }

        public override int apply(Funcdata data)
        {
            ProtoParameter outparam = data.getFuncProto().getOutput();
            if ((!outparam.isTypeLocked()) || outparam.isSizeTypeLocked()) {
                PcodeOp? op = data.getFirstReturnOp();
                List<Varnode> vnlist = new List<Varnode>();
                if (op != (PcodeOp)null) {
                    for (int i = 1; i < op.numInput(); ++i)
                        vnlist.Add(op.getIn(i));
                }
                if (data.isHighOn())
                    data.getFuncProto().updateOutputTypes(vnlist);
                else
                    data.getFuncProto().updateOutputNoTypes(vnlist, data.getArch().types);
            }
            return 0;
        }
    }
}
