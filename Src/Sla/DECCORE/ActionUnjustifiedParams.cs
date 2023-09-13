
namespace Sla.DECCORE
{
    /// \brief Adjust improperly justified parameters
    ///
    /// Scan through all inputs, find Varnodes that look like improperly justified input parameters
    /// create a new full input, and change the old partial input to be formed as a OpCode.CPUI_SUBPIECE of the
    /// full input
    internal class ActionUnjustifiedParams : Action
    {
        public ActionUnjustifiedParams(string g)
            : base(0,"unjustparams", g)
        {
        }

        public override Action? clone(ActionGroupList grouplist)
        {
            return (!grouplist.contains(getGroup())) ? null : new ActionUnjustifiedParams(getGroup());
        }

        public override int apply(Funcdata data)
        {
            FuncProto proto = data.getFuncProto();

            IEnumerator<Varnode> iter = data.beginDef(Varnode.varnode_flags.input);
            // IEnumerator<Varnode> enditer = data.endDef(Varnode.varnode_flags.input);

            while (iter.MoveNext()) {
                Varnode vn = iter.Current;
                VarnodeData vdata;
                if (!proto.unjustifiedInputParam(vn.getAddr(), vn.getSize(), vdata))
                    continue;

                bool newcontainer;
                do {
                    newcontainer = false;
                    IEnumerator<Varnode> begiter = data.beginDef(Varnode.varnode_flags.input);
                    IEnumerator<Varnode> iter2 = iter;
                    bool overlaps = false;
                    while (iter2 != begiter) {
                        --iter2;
                        vn = iter2.Current;
                        if (vn.getSpace() != vdata.space) continue;
                        // Last offset in varnode
                        ulong offset = vn.getOffset() + vn.getSize() - 1;
                        if ((offset >= vdata.offset) && (vn.getOffset() < vdata.offset)) {
                            // If there is overlap that extends size
                            overlaps = true;
                            ulong endpoint = vdata.offset + vdata.size;
                            vdata.offset = vn.getOffset();
                            vdata.size = endpoint - vdata.offset;
                        }
                    }
                    if (!overlaps)
                        // Found no additional overlaps, go with current justified container
                        break;
                    // If there were overlaps, container may no longer be justified
                    newcontainer = proto.unjustifiedInputParam(vdata.getAddr(), vdata.size, vdata);
                } while (newcontainer);

                data.adjustInputVarnodes(vdata.getAddr(), vdata.size);
                // Reset iterator because of additions and deletions
                iter = data.beginDef(Varnode.varnode_flags.input, vdata.getAddr());
                enditer = data.endDef(Varnode.varnode_flags.input);
                count += 1;
            }
            return 0;
        }
    }
}
