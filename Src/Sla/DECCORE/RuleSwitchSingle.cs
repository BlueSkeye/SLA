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
    internal class RuleSwitchSingle : Rule
    {
        public RuleSwitchSingle(string g)
            : base(g,0,"switchsingle")
        {
        }

        public override Rule clone(ActionGroupList grouplist)
        {
            if (!grouplist.contains(getGroup())) return (Rule*)0;
            return new RuleSwitchSingle(getGroup());
        }

        /// \class RuleSwitchSingle
        /// \brief Convert BRANCHIND with only one computed destination to a BRANCH
        public override void getOpList(List<uint> oplist)
        {
            oplist.push_back(CPUI_BRANCHIND);
        }

        public override int applyOp(PcodeOp op, Funcdata data)
        {
            BlockBasic* bb = op.getParent();
            if (bb.sizeOut() != 1) return 0;

            JumpTable* jt = data.findJumpTable(op);
            if (jt == (JumpTable*)0) return 0;
            if (jt.numEntries() == 0) return 0;
            if (!jt.isLabelled()) return 0; // Labels must be recovered (as this discovers multistage issues)
            Address addr = jt.getAddressByIndex(0);
            bool needwarning = false;
            bool allcasesmatch = false;
            if (jt.numEntries() != 1)
            {
                needwarning = true;
                allcasesmatch = true;
                for (int i = 1; i < jt.numEntries(); ++i)
                {
                    if (jt.getAddressByIndex(i) != addr)
                    {
                        allcasesmatch = false;
                        break;
                    }
                }
            }

            if (!op.getIn(0).isConstant())
                needwarning = true;
            // If the switch variable is a constant this is final
            // confirmation that the switch has only one destination
            // otherwise this may indicate some other problem

            if (needwarning)
            {
                ostringstream s;
                s << "Switch with 1 destination removed at ";
                op.getAddr().printRaw(s);
                if (allcasesmatch)
                {
                    s << " : " << dec << jt.numEntries() << " cases all go to same destination";
                }
                data.warningHeader(s.str());
            }

            // Convert the BRANCHIND to just a branch
            data.opSetOpcode(op, CPUI_BRANCH);
            // Stick in the coderef of the single jumptable entry
            data.opSetInput(op, data.newCodeRef(addr), 0);
            data.removeJumpTable(jt);
            data.getStructure().clear();    // Get rid of any block switch structures
            return 1;
        }
    }
}
