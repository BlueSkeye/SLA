using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Sla.DECCORE
{
    /// \brief Eliminate locally constant indirect calls
    internal class ActionDeindirect : Action
    {
        /// Constructor
        public ActionDeindirect(string g)
            : base(0,"deindirect", g)
        {
        }

        public override Action? clone(ActionGroupList grouplist)
        {
            return (!grouplist.contains(getGroup())) ? null : new ActionDeindirect(getGroup());
        }

        public override int apply(Funcdata data)
        {
            FuncCallSpecs* fc;
            PcodeOp* op;
            Varnode* vn;

            for (int i = 0; i < data.numCalls(); ++i)
            {
                fc = data.getCallSpecs(i);
                op = fc.getOp();
                if (op.code() != CPUI_CALLIND) continue;
                vn = op.getIn(0);
                while (vn.isWritten() && (vn.getDef().code() == CPUI_COPY))
                    vn = vn.getDef().getIn(0);
                if (vn.isPersist() && vn.isExternalRef())
                { // Check for possible external reference
                    Funcdata* newfd = data.getScopeLocal().getParent().queryExternalRefFunction(vn.getAddr());
                    if (newfd != (Funcdata*)0)
                    {
                        fc.deindirect(data, newfd);
                        count += 1;
                        continue;
                    }
                }
                else if (vn.isConstant())
                {
                    AddrSpace* sp = data.getAddress().getSpace(); // Assume function is in same space as calling function
                                                                  // Convert constant to a byte address in this space
                    ulong offset = AddrSpace::addressToByte(vn.getOffset(), sp.getWordSize());
                    int align = data.getArch().funcptr_align;
                    if (align != 0)
                    {       // If we know function pointer should be aligned
                        offset >>= align;   // Remove any encoding bits before querying for the function
                        offset <<= align;
                    }
                    Address codeaddr(sp, offset);
                    Funcdata* newfd = data.getScopeLocal().getParent().queryFunction(codeaddr);
                    if (newfd != (Funcdata*)0)
                    {
                        fc.deindirect(data, newfd);
                        count += 1;
                        continue;
                    }
                }
                if (data.hasTypeRecoveryStarted())
                {
                    // Check for a function pointer that has an attached prototype
                    Datatype* ct = op.getIn(0).getTypeReadFacing(op);
                    if ((ct.getMetatype() == TYPE_PTR) &&
                    (((TypePointer*)ct).getPtrTo().getMetatype() == TYPE_CODE))
                    {
                        TypeCode* tc = (TypeCode*)((TypePointer*)ct).getPtrTo();
                        FuncProto* fp = tc.getPrototype();
                        if (fp != (FuncProto*)0)
                        {
                            if (!fc.isInputLocked())
                            {
                                // We use isInputLocked as a test of whether the
                                // function pointer prototype has been applied before
                                fc.forceSet(data, *fp);
                                count += 1;
                            }
                        }
                        // FIXME: If fc's input IS locked presumably this means
                        // that this prototype is already set, but it MIGHT mean
                        // we have conflicting locked prototypes
                    }
                }
            }
            return 0;
        }
    }
}
