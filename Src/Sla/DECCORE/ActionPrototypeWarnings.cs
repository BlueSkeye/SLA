using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Sla.DECCORE
{
    /// \brief Add warnings for prototypes that aren't modeled properly
    internal class ActionPrototypeWarnings : Action
    {
        public ActionPrototypeWarnings(string g)
            : base(rule_onceperfunc,"prototypewarnings", g)
        {
        }

        public override Action? clone(ActionGroupList grouplist)
        {
            return (!grouplist.contains(getGroup())) ? null : new ActionPrototypeWarnings(getGroup());
        }

        public override int apply(Funcdata data)
        {
            List<string> overridemessages;
            data.getOverride().generateOverrideMessages(overridemessages, data.getArch());
            for (int i = 0; i < overridemessages.size(); ++i)
                data.warningHeader(overridemessages[i]);

            FuncProto & ourproto(data.getFuncProto());
            if (ourproto.hasInputErrors())
            {
                data.warningHeader("Cannot assign parameter locations for this function: Prototype may be inaccurate");
            }
            if (ourproto.hasOutputErrors())
            {
                data.warningHeader("Cannot assign location of return value for this function: Return value may be inaccurate");
            }
            if (ourproto.isModelUnknown())
            {
                ostringstream s;
                s << "Unknown calling convention";
                if (ourproto.printModelInDecl())
                    s << ": " << ourproto.getModelName();
                if (!ourproto.hasCustomStorage() && (ourproto.isInputLocked() || ourproto.isOutputLocked()))
                    s << " -- yet parameter storage is locked";
                data.warningHeader(s.str());
            }
            int numcalls = data.numCalls();
            for (int i = 0; i < numcalls; ++i)
            {
                FuncCallSpecs fc = data.getCallSpecs(i);
                Funcdata* fd = fc.getFuncdata();
                if (fc.hasInputErrors())
                {
                    ostringstream s;
                    s << "Cannot assign parameter location for function ";
                    if (fd != (Funcdata)null)
                        s << fd.getName();
                    else
                        s << "<indirect>";
                    s << ": Prototype may be inaccurate";
                    data.warning(s.str(), fc.getEntryAddress());
                }
                if (fc.hasOutputErrors())
                {
                    ostringstream s;
                    s << "Cannot assign location of return value for function ";
                    if (fd != (Funcdata)null)
                        s << fd.getName();
                    else
                        s << "<indirect>";
                    s << ": Return value may be inaccurate";
                    data.warning(s.str(), fc.getEntryAddress());
                }
            }
            return 0;
        }
    }
}
