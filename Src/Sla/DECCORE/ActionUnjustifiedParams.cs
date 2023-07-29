using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Sla.DECCORE
{
    /// \brief Adjust improperly justified parameters
    ///
    /// Scan through all inputs, find Varnodes that look like improperly justified input parameters
    /// create a new full input, and change the old partial input to be formed as a CPUI_SUBPIECE of the
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
            VarnodeDefSet::const_iterator iter, enditer;
            FuncProto & proto(data.getFuncProto());

            iter = data.beginDef(Varnode::input);
            enditer = data.endDef(Varnode::input);

            while (iter != enditer)
            {
                Varnode* vn = *iter++;
                VarnodeData vdata;
                if (!proto.unjustifiedInputParam(vn->getAddr(), vn->getSize(), vdata)) continue;

                bool newcontainer;
                do
                {
                    newcontainer = false;
                    VarnodeDefSet::const_iterator begiter, iter2;
                    begiter = data.beginDef(Varnode::input);
                    iter2 = iter;
                    bool overlaps = false;
                    while (iter2 != begiter)
                    {
                        --iter2;
                        vn = *iter2;
                        if (vn->getSpace() != vdata.space) continue;
                        uintb offset = vn->getOffset() + vn->getSize() - 1; // Last offset in varnode
                        if ((offset >= vdata.offset) && (vn->getOffset() < vdata.offset))
                        { // If there is overlap that extends size
                            overlaps = true;
                            uintb endpoint = vdata.offset + vdata.size;
                            vdata.offset = vn->getOffset();
                            vdata.size = endpoint - vdata.offset;
                        }
                    }
                    if (!overlaps) break;   // Found no additional overlaps, go with current justified container
                                            // If there were overlaps, container may no longer be justified
                    newcontainer = proto.unjustifiedInputParam(vdata.getAddr(), vdata.size, vdata);
                } while (newcontainer);

                data.adjustInputVarnodes(vdata.getAddr(), vdata.size);
                // Reset iterator because of additions and deletions
                iter = data.beginDef(Varnode::input, vdata.getAddr());
                enditer = data.endDef(Varnode::input);
                count += 1;
            }
            return 0;
        }
    }
}
