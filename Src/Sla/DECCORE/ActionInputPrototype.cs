using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Sla.DECCORE
{
    /// \brief Calculate the prototype for the function.
    ///
    /// If the prototype wasn't originally known, the discovered input Varnodes are analyzed
    /// to determine a prototype based on the prototype model.
    internal class ActionInputPrototype : Action
    {
        public ActionInputPrototype(string g)
            : base(rule_onceperfunc,"inputprototype", g)
        {
        }

        public override Action? clone(ActionGroupList grouplist)
        {
            return (!grouplist.contains(getGroup())) ? null : new ActionInputPrototype(getGroup());
        }

        public override int apply(Funcdata data)
        {
            List<Varnode> triallist = new List<Varnode>();
            ParamActive active = new ParamActive(false);
            Varnode vn;

            // Clear any unlocked local variables because these are
            // getting cleared anyway in the restructure and may be
            // using symbol names that we want
            data.getScopeLocal().clearUnlockedCategory(-1);
            data.getFuncProto().clearUnlockedInput();
            if (!data.getFuncProto().isInputLocked())
            {
                VarnodeDefSet::const_iterator iter, enditer;
                iter = data.beginDef(Varnode::input);
                enditer = data.endDef(Varnode::input);
                while (iter != enditer)
                {
                    vn = *iter;
                    ++iter;
                    if (data.getFuncProto().possibleInputParam(vn.getAddr(), vn.getSize()))
                    {
                        int slot = active.getNumTrials();
                        active.registerTrial(vn.getAddr(), vn.getSize());
                        if (!vn.hasNoDescend())
                            active.getTrial(slot).markActive(); // Mark as active if it has descendants
                        triallist.Add(vn);
                    }
                }
                data.getFuncProto().resolveModel(&active);
                data.getFuncProto().deriveInputMap(&active); // Derive the correct prototype from trials
                                                             // Create any unreferenced input varnodes
                for (int i = 0; i < active.getNumTrials(); ++i)
                {
                    ParamTrial & paramtrial(active.getTrial(i));
                    if (paramtrial.isUnref() && paramtrial.isUsed())
                    {
                        vn = data.newVarnode(paramtrial.getSize(), paramtrial.getAddress());
                        vn = data.setInputVarnode(vn);
                        int slot = triallist.size();
                        triallist.Add(vn);
                        paramtrial.setSlot(slot + 1);
                    }
                }
                if (data.isHighOn())
                    data.getFuncProto().updateInputTypes(data, triallist, &active);
                else
                    data.getFuncProto().updateInputNoTypes(data, triallist, &active);
            }
            data.clearDeadVarnodes();
#if OPACTION_DEBUG
            if ((flags & rule_debug) == 0) return 0;
            ostringstream s;
            data.getScopeLocal().printEntries(s);
            data.getArch().printDebug(s.str());
#endif
            return 0;
        }
    }
}
