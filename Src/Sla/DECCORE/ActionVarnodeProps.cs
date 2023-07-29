using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Sla.DECCORE
{
    /// \brief Transform based on Varnode properties, such as \e read-only and \e volatile
    ///
    /// This performs various transforms that are based on Varnode properties.
    ///   - Read-only Varnodes are converted to the underlying constant
    ///   - Volatile Varnodes are converted read/write functions
    ///   - Varnodes whose values are not consumed are replaced with constant 0 Varnodes
    internal class ActionVarnodeProps : Action
    {
        /// Constructor
        public ActionVarnodeProps(string g)
            : base(0, "varnodeprops", g)
        {
        }

        public override Action? clone(ActionGroupList grouplist)
        {
            return (!grouplist.contains(getGroup())) ? null : new ActionVarnodeProps(getGroup());
        }
        
        public override int apply(Funcdata data)
        {
            Architecture* glb = data.getArch();
            bool cachereadonly = glb.readonlypropagate;
            int pass = data.getHeritagePass();
            VarnodeLocSet::const_iterator iter;
            Varnode* vn;

            iter = data.beginLoc();
            while (iter != data.endLoc())
            {
                vn = *iter++;       // Advance iterator in case vn is deleted
                if (vn.isAnnotation()) continue;
                int vnSize = vn.getSize();
                if (vn.isAutoLiveHold())
                {
                    if (pass > 0)
                    {
                        if (vn.isWritten())
                        {
                            PcodeOp* loadOp = vn.getDef();
                            if (loadOp.code() == CPUI_LOAD)
                            {
                                Varnode* ptr = loadOp.getIn(1);
                                if (ptr.isConstant() || ptr.isReadOnly())
                                    continue;
                                if (ptr.isWritten())
                                {
                                    PcodeOp* copyOp = ptr.getDef();
                                    if (copyOp.code() == CPUI_COPY)
                                    {
                                        ptr = copyOp.getIn(0);
                                        if (ptr.isConstant() || ptr.isReadOnly())
                                            continue;
                                    }
                                }
                            }
                        }
                        vn.clearAutoLiveHold();
                        count += 1;
                    }
                }
                else if (vn.hasActionProperty())
                {
                    if (cachereadonly && vn.isReadOnly())
                    {
                        if (data.fillinReadOnly(vn)) // Try to replace vn with its lookup in LoadImage
                            count += 1;
                    }
                    else if (vn.isVolatile())
                        if (data.replaceVolatile(vn))
                            count += 1;     // Try to replace vn with pcode op
                }
                else if (((vn.getNZMask() & vn.getConsume()) == 0) && (vnSize <= sizeof(ulong)))
                {
                    // FIXME: ulong should be arbitrary precision
                    if (vn.isConstant()) continue; // Don't replace a constant
                    if (vn.isWritten())
                    {
                        if (vn.getDef().code() == CPUI_COPY)
                        {
                            if (vn.getDef().getIn(0).isConstant())
                            {
                                // Don't replace a COPY 0, with a zero, let
                                // constant propagation do that. This prevents
                                // an infinite recursion
                                if (vn.getDef().getIn(0).getOffset() == 0)
                                    continue;
                            }
                        }
                    }
                    if (!vn.hasNoDescend())
                    {
                        data.totalReplaceConstant(vn, 0);
                        count += 1;
                    }
                }
            }
            data.setLanedRegGenerated();
            return 0;
        }
    }
}
