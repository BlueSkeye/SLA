﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Sla.DECCORE
{
    /// \brief Mark Varnodes built out of \e legal parameters
    ///
    /// Label a varnode with the \b directwrite attribute if:
    /// that varnode can trace at least part of its data-flow ancestry to legal inputs,
    /// where \b legal inputs include:  globals, spacebase registers, and normal function parameters.
    /// The directwrite attribute is set on these inputs initially and then propagated
    /// to other varnodes through all other ops except CPUI_INDIRECT. The attribute propagates
    /// through CPUI_INDIRECT depending on the setting of -propagateIndirect-.
    /// For normal decompilation, propagation through CPUI_INDIRECTs is important for stack and other
    /// high-level addrtied variables that need to hold their value over ranges where they are not
    /// accessed directly. But propagation adds unnecessary clutter for normalization style analysis.
    internal class ActionDirectWrite : Action
    {
        /// Propagate thru CPUI_INDIRECT ops
        private bool propagateIndirect;

        /// Constructor
        public ActionDirectWrite(string g, bool prop)
            : base(0, "directwrite", g)
        {
            propagateIndirect = prop;
        }

        public override Action? clone(ActionGroupList grouplist)
        {
            return (!grouplist.contains(getGroup()))
                ? null
                : new ActionDirectWrite(getGroup(), propagateIndirect);
        }
        public override int apply(Funcdata data)
        {
            VarnodeLocSet::const_iterator iter;
            list<PcodeOp*>::const_iterator oiter;
            Varnode* vn,*dvn;
            PcodeOp* op;
            List<Varnode*> worklist;

            // Collect legal inputs and other auto direct writes
            for (iter = data.beginLoc(); iter != data.endLoc(); ++iter)
            {
                vn = *iter;
                vn.clearDirectWrite();
                if (vn.isInput())
                {
                    if (vn.isPersist() || vn.isSpacebase())
                    {
                        vn.setDirectWrite();
                        worklist.Add(vn);
                    }
                    else if (data.getFuncProto().possibleInputParam(vn.getAddr(), vn.getSize()))
                    {
                        vn.setDirectWrite();
                        worklist.Add(vn);
                    }
                }
                else if (vn.isWritten())
                {
                    op = vn.getDef();
                    if (!op.isMarker())
                    {
                        if (vn.isPersist())
                        {
                            // Anything that writes to a global variable (in a real way) is considered a direct write
                            vn.setDirectWrite();
                            worklist.Add(vn);
                        }
                        else if (op.code() == CPUI_COPY)
                        {   // For most COPYs, do not consider it a direct write
                            if (vn.isStackStore())
                            {       // But, if the original operation was really a CPUI_STORE
                                Varnode* invn = op.getIn(0);   // Trace COPY source
                                if (invn.isWritten())
                                {       // Through possible multiple COPYs
                                    PcodeOp* curop = invn.getDef();
                                    if (curop.code() == CPUI_COPY)
                                        invn = curop.getIn(0);
                                }
                                if (invn.isWritten() && invn.getDef().isMarker())
                                {   // if source is from an INDIRECT
                                    vn.setDirectWrite();                   // then treat this as a direct write
                                    worklist.Add(vn);
                                }
                            }
                        }
                        else if ((op.code() != CPUI_PIECE) && (op.code() != CPUI_SUBPIECE))
                        {
                            // Anything that writes to a variable in a way that isn't some form of COPY is a direct write
                            vn.setDirectWrite();
                            worklist.Add(vn);
                        }
                    }
                    else if (!propagateIndirect && op.code() == CPUI_INDIRECT)
                    {
                        Varnode* outvn = op.getOut();
                        if (op.getIn(0).getAddr() != outvn.getAddr())    // Check if storage address changes from input to output
                            vn.setDirectWrite();                   // Indicates an active COPY, which is a direct write
                        else if (outvn.isPersist())                // Value must be present at global storage at point call is made
                            vn.setDirectWrite();                   //   so treat as direct write
                                                                    // We do NOT add vn to worklist as INDIRECT otherwise does not propagate
                    }
                }
                else if (vn.isConstant())
                {
                    if (!vn.isIndirectZero())
                    {
                        vn.setDirectWrite();
                        worklist.Add(vn);
                    }
                }
            }
            // Let legalness taint
            while (!worklist.empty())
            {
                vn = worklist.GetLastItem();
                worklist.pop_back();
                for (oiter = vn.beginDescend(); oiter != vn.endDescend(); ++oiter)
                {
                    op = *oiter;
                    if (!op.isAssignment()) continue;
                    dvn = op.getOut();
                    if (!dvn.isDirectWrite())
                    {
                        dvn.setDirectWrite();
                        // For call based INDIRECTs, output is marked, but does not propagate depending on setting
                        if (propagateIndirect || op.code() != CPUI_INDIRECT || op.isIndirectStore())
                            worklist.Add(dvn);
                    }
                }
            }
            return 0;
        }
    }
}
