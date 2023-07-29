﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Sla.DECCORE
{
    /// \brief Find a prototype for each sub-function
    ///
    /// This loads prototype information, if it exists for each sub-function. If no explicit
    /// prototype exists, a default is selected.  If the prototype model specifies
    /// \e uponreturn injection, the p-code is injected at this time.
    internal class ActionDefaultParams : Action
    {
        public ActionDefaultParams(string g)
            : base(rule_onceperfunc,"defaultparams", g)
        {
        }

        public override Action? clone(ActionGroupList grouplist)
        {
            return (!grouplist.contains(getGroup())) ? null : new ActionDefaultParams(getGroup());
        }

        public override int apply(Funcdata data)
        {
            vector<Varnode*> triallist;
            ParamActive active(false);
            Varnode* vn;

            // Clear any unlocked local variables because these are
            // getting cleared anyway in the restructure and may be
            // using symbol names that we want
            data.getScopeLocal()->clearUnlockedCategory(-1);
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
                    if (data.getFuncProto().possibleInputParam(vn->getAddr(), vn->getSize()))
                    {
                        int4 slot = active.getNumTrials();
                        active.registerTrial(vn->getAddr(), vn->getSize());
                        if (!vn->hasNoDescend())
                            active.getTrial(slot).markActive(); // Mark as active if it has descendants
                        triallist.push_back(vn);
                    }
                }
                data.getFuncProto().resolveModel(&active);
                data.getFuncProto().deriveInputMap(&active); // Derive the correct prototype from trials
                                                             // Create any unreferenced input varnodes
                for (int4 i = 0; i < active.getNumTrials(); ++i)
                {
                    ParamTrial & paramtrial(active.getTrial(i));
                    if (paramtrial.isUnref() && paramtrial.isUsed())
                    {
                        vn = data.newVarnode(paramtrial.getSize(), paramtrial.getAddress());
                        vn = data.setInputVarnode(vn);
                        int4 slot = triallist.size();
                        triallist.push_back(vn);
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
            data.getScopeLocal()->printEntries(s);
            data.getArch()->printDebug(s.str());
#endif
            return 0;
        }
    }
}