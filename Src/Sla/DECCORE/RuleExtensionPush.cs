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
    internal class RuleExtensionPush : Rule
    {
        public RuleExtensionPush(string g)
            : base(g, 0, "extensionpush")
        {
        }

        public override Rule clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule*)0;
            return new RuleExtensionPush(getGroup());
        }

        /// \class RuleExtensionPush
        /// \brief Duplicate CPUI_INT_ZEXT and CPUI_INT_SEXT operations if the result is used in multiple pointer calculations
        ///
        /// By making the extension operation part of each pointer calculation (where it is usually an implied cast),
        /// we can frequently eliminate an explicit variable that would just hold the extension.
        public override void getOpList(List<uint> &oplist)
        {
            oplist.Add(CPUI_INT_ZEXT);
            oplist.Add(CPUI_INT_SEXT);
        }

        public override int applyOp(PcodeOp op, Funcdata data)
        {
            Varnode* inVn = op.getIn(0);
            if (inVn.isConstant()) return 0;
            if (inVn.isAddrForce()) return 0;
            if (inVn.isAddrTied()) return 0;
            Varnode* outVn = op.getOut();
            if (outVn.isTypeLock() || outVn.isNameLock()) return 0;
            if (outVn.isAddrForce() || outVn.isAddrTied()) return 0;
            list<PcodeOp*>::const_iterator iter;
            int addcount = 0;      // Number of INT_ADD descendants
            int ptrcount = 0;      // Number of PTRADD descendants
            for (iter = outVn.beginDescend(); iter != outVn.endDescend(); ++iter)
            {
                PcodeOp* decOp = *iter;
                OpCode opc = decOp.code();
                if (opc == CPUI_PTRADD)
                {
                    // This extension will likely be hidden
                    ptrcount += 1;
                }
                else if (opc == CPUI_INT_ADD)
                {
                    PcodeOp* subOp = decOp.getOut().loneDescend();
                    if (subOp == (PcodeOp)null || subOp.code() != CPUI_PTRADD)
                        return 0;
                    addcount += 1;
                }
                else
                {
                    return 0;
                }
            }
            if ((addcount + ptrcount) <= 1) return 0;
            if (addcount > 0)
            {
                if (op.getIn(0).loneDescend() != (PcodeOp)null) return 0;
            }
            RulePushPtr::duplicateNeed(op, data);       // Duplicate the extension to all result descendants
            return 1;
        }
    }
}
