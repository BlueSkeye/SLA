using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Sla.DECCORE
{
    internal class RuleStoreVarnode : Rule
    {
        public RuleStoreVarnode(string g)
            : base(g, 0, "storevarnode")
        {
        }

        public override Rule clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule*)0;
            return new RuleStoreVarnode(getGroup());
        }

        /// \class RuleStoreVarnode
        /// \brief Convert STORE operations using a constant offset to COPY
        ///
        /// The pointer can either be a constant offset into the STORE's specified address space,
        /// or it can be a \e spacebase register plus an offset, in which case it points into
        /// the \e spacebase register's address space.
        public override void getOpList(List<uint> oplist)
        {
            oplist.Add(CPUI_STORE);
        }

        public override int applyOp(PcodeOp op, Funcdata data)
        {
            int size;
            AddrSpace* baseoff;
            ulong offoff;

            baseoff = RuleLoadVarnode::checkSpacebase(data.getArch(), op, offoff);
            if (baseoff == (AddrSpace)null) return 0;

            size = op.getIn(2).getSize();
            offoff = AddrSpace::addressToByte(offoff, baseoff.getWordSize());
            Address addr(baseoff, offoff);
            data.newVarnodeOut(size, addr, op);
            op.getOut().setStackStore();  // Mark as originally coming from CPUI_STORE
            data.opRemoveInput(op, 1);
            data.opRemoveInput(op, 0);
            data.opSetOpcode(op, CPUI_COPY);
            return 1;
        }
    }
}
