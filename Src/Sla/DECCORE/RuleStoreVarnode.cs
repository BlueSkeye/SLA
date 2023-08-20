using Sla.CORE;
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

        public override Rule? clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule)null;
            return new RuleStoreVarnode(getGroup());
        }

        /// \class RuleStoreVarnode
        /// \brief Convert STORE operations using a constant offset to COPY
        ///
        /// The pointer can either be a constant offset into the STORE's specified address space,
        /// or it can be a \e spacebase register plus an offset, in which case it points into
        /// the \e spacebase register's address space.
        public override void getOpList(List<OpCode> oplist)
        {
            oplist.Add(OpCode.CPUI_STORE);
        }

        public override int applyOp(PcodeOp op, Funcdata data)
        {
            int size;
            ulong offoff;

            AddrSpace? baseoff = RuleLoadVarnode.checkSpacebase(data.getArch(), op, out offoff);
            if (baseoff == (AddrSpace)null) return false;

            size = op.getIn(2).getSize();
            offoff = AddrSpace.addressToByte(offoff, baseoff.getWordSize());
            Address addr = new Address(baseoff, offoff);
            data.newVarnodeOut(size, addr, op);
            op.getOut().setStackStore();  // Mark as originally coming from OpCode.CPUI_STORE
            data.opRemoveInput(op, 1);
            data.opRemoveInput(op, 0);
            data.opSetOpcode(op, OpCode.CPUI_COPY);
            return true;
        }
    }
}
