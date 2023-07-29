using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Sla.DECCORE
{
    /// \brief Lay down locked input and output data-type information.
    ///
    /// Build forced input/output Varnodes and extend them as appropriate.
    /// Set types on output forced Varnodes (input types are set automatically by the database).
    /// Initialize output recovery process.
    internal class ActionPrototypeTypes : Action
    {
        /// \brief Extend Varnode inputs to match prototype model.
        ///
        /// For prototype models that assume input variables are already extended in some way,
        /// insert the appropriate extension operation to allow correct small-size input
        /// Varnode to exist.
        /// \param data is the function being analyzed
        /// \param invn is the given (small) input Varnode
        /// \param param is the matching symbol info for the Varnode
        /// \param topbl is the entry block for the function
        public void extendInput(Funcdata data, Varnode invn, ProtoParameter param, BlockBasic topbl)
        {
            VarnodeData vdata;
            OpCode res = data.getFuncProto().assumedInputExtension(invn.getAddr(), invn.getSize(), vdata);
            if (res == CPUI_COPY) return;       // no extension
            if (res == CPUI_PIECE)
            {   // Do an extension based on type of parameter
                if (param.getType().getMetatype() == TYPE_INT)
                    res = CPUI_INT_SEXT;
                else
                    res = CPUI_INT_ZEXT;
            }
            PcodeOp* op = data.newOp(1, topbl.getStart());
            data.newVarnodeOut(vdata.size, vdata.getAddr(), op);
            data.opSetOpcode(op, res);
            data.opSetInput(op, invn, 0);
            data.opInsertBegin(op, topbl);
        }

        public ActionPrototypeTypes(string g)
            : base(rule_onceperfunc,"prototypetypes", g)
        {
        }

        public override Action? clone(ActionGroupList grouplist)
        {
            return (!grouplist.contains(getGroup())) ? null : new ActionPrototypeTypes(getGroup());
        }
    
        public override int apply(Funcdata data)
        {
            list<PcodeOp*>::const_iterator iter, iterend;

            // Set the evaluation prototype if we are not already locked
            ProtoModel* evalfp = data.getArch().evalfp_current;
            if (evalfp == (ProtoModel*)0)
                evalfp = data.getArch().defaultfp;
            if ((!data.getFuncProto().isModelLocked()) && !data.getFuncProto().hasMatchingModel(evalfp))
                data.getFuncProto().setModel(evalfp);
            if (data.getFuncProto().hasThisPointer())
                data.prepareThisPointer();

            iterend = data.endOp(CPUI_RETURN);

            // Strip the indirect register from all RETURN ops
            // (Because we don't want to see this compiler
            // mechanism in the high-level C output)
            for (iter = data.beginOp(CPUI_RETURN); iter != iterend; ++iter)
            {
                PcodeOp* op = *iter;
                if (op.isDead()) continue;
                if (!op.getIn(0).isConstant())
                {
                    Varnode* vn = data.newConstant(op.getIn(0).getSize(), 0);
                    data.opSetInput(op, vn, 0);
                }
            }

            if (data.getFuncProto().isOutputLocked())
            {
                ProtoParameter* outparam = data.getFuncProto().getOutput();
                if (outparam.getType().getMetatype() != TYPE_VOID)
                {
                    for (iter = data.beginOp(CPUI_RETURN); iter != iterend; ++iter)
                    {
                        PcodeOp* op = *iter;
                        if (op.isDead()) continue;
                        if (op.getHaltType() != 0) continue;
                        Varnode* vn = data.newVarnode(outparam.getSize(), outparam.getAddress());
                        data.opInsertInput(op, vn, op.numInput());
                        vn.updateType(outparam.getType(), true, true);
                    }
                }
            }
            else
                data.initActiveOutput(); // Initiate gathering potential return values

            AddrSpace* spc = data.getArch().getDefaultCodeSpace();
            if (spc.isTruncated())
            {
                // For truncated spaces we need a zext op, from the truncated stack pointer
                // into the full stack pointer
                AddrSpace* stackspc = data.getArch().getStackSpace();
                BlockBasic* topbl = (BlockBasic*)0;
                if (data.getBasicBlocks().getSize() > 0)
                    topbl = (BlockBasic*)data.getBasicBlocks().getBlock(0);
                if ((stackspc != (AddrSpace*)0) && (topbl != (BlockBasic*)0))
                {
                    for (int i = 0; i < stackspc.numSpacebase(); ++i)
                    {
                        VarnodeData &fullReg(stackspc.getSpacebaseFull(i));
                        VarnodeData &truncReg(stackspc.getSpacebase(i));
                        Varnode* invn = data.newVarnode(truncReg.size, truncReg.getAddr());
                        invn = data.setInputVarnode(invn);
                        PcodeOp* extop = data.newOp(1, topbl.getStart());
                        data.newVarnodeOut(fullReg.size, fullReg.getAddr(), extop);
                        data.opSetOpcode(extop, CPUI_INT_ZEXT);
                        data.opSetInput(extop, invn, 0);
                        data.opInsertBegin(extop, topbl);
                    }
                }
            }

            // Force locked inputs to exist as varnodes

            // This is needed if we want to force a big input to exist
            // but only part of it is getting used. This is allows
            // a SUBPIECE instruction to get built with the big variable
            // as input and the part getting used as output.
            if (data.getFuncProto().isInputLocked())
            {

                int ptr_size = spc.isTruncated() ? spc.getAddrSize() : 0; // Check if we need to do pointer trimming
                BlockBasic* topbl = (BlockBasic*)0;
                if (data.getBasicBlocks().getSize() > 0)
                    topbl = (BlockBasic*)data.getBasicBlocks().getBlock(0);

                int numparams = data.getFuncProto().numParams();
                for (int i = 0; i < numparams; ++i)
                {
                    ProtoParameter* param = data.getFuncProto().getParam(i);
                    Varnode* vn = data.newVarnode(param.getSize(), param.getAddress());
                    vn = data.setInputVarnode(vn);
                    vn.setLockedInput();
                    if (topbl != (BlockBasic*)0)
                        extendInput(data, vn, param, topbl);
                    if (ptr_size > 0)
                    {
                        Datatype* ct = param.getType();
                        if ((ct.getMetatype() == TYPE_PTR) && (ct.getSize() == ptr_size))
                            vn.setPtrFlow();
                    }
                }
            }
            return 0;
        }
    }
}
