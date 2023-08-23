using Sla.CORE;

namespace Sla.DECCORE
{
    /// \brief Prepare for data-flow analysis of function parameters
    ///
    /// If exact prototypes are known for sub-functions, insert the appropriate
    /// Varnodes to match the parameters. If not known, prepare the sub-function for
    /// the parameter recovery process.
    internal class ActionFuncLink : Action
    {
        // friend class ActionFuncLinkOutOnly;
        /// \brief Set up the parameter recovery process for a single sub-function call
        ///
        /// If the prototype is known (locked), insert stub Varnodes
        /// If the prototype is varargs (dotdotdot), set up recovery of variable Varnodes
        /// \param fc is the given sub-function
        /// \param data is the function being analyzed
        private static void funcLinkInput(FuncCallSpecs fc, Funcdata data)
        {
            bool inputlocked = fc.isInputLocked();
            bool varargs = fc.isDotdotdot();
            AddrSpace spacebase = fc.getSpacebase();  // Non-zero spacebase indicates we need a stackplaceholder
            ParamActive active = fc.getActiveInput();

            if ((!inputlocked) || varargs)
                fc.initActiveInput();
            if (inputlocked) {
                PcodeOp op = fc.getOp();
                int numparam = fc.numParams();
                bool setplaceholder = varargs;
                for (int i = 0; i < numparam; ++i) {
                    ProtoParameter param = fc.getParam(i);
                    active.registerTrial(param.getAddress(), param.getSize());
                    active.getTrial(i).markActive(); // Parameter is not optional
                    if (varargs) {
                        active.getTrial(i).setFixedPosition(i);
                    }
                    AddrSpace spc = param.getAddress().getSpace();
                    ulong off = param.getAddress().getOffset();
                    int sz = param.getSize();
                    if (spc.getType() == spacetype.IPTR_SPACEBASE) {
                        // Param is stack relative
                        Varnode loadval = data.opStackLoad(spc, off, sz, op, (Varnode)null,
                            false);
                        data.opInsertInput(op, loadval, op.numInput());
                        if (!setplaceholder) {
                            setplaceholder = true;
                            loadval.setSpacebasePlaceholder();
                            spacebase = (AddrSpace)null;  // With a locked stack parameter, we don't need a stackplaceholder
                        }
                    }
                    else
                        data.opInsertInput(op, data.newVarnode(param.getSize(), param.getAddress()), op.numInput());
                }
            }
            if (spacebase != (AddrSpace)null)
                // If we need it, create the stackplaceholder
                fc.createPlaceholder(data, spacebase);
        }

        /// \brief Set up the return value recovery process for a single sub-function call
        ///
        /// If the prototype is known(locked), insert an output Varnode on the call
        /// If the prototype is unknown set-up the ParamActive, so that outputs will be "gathered"
        /// \param fc is the given sub-function
        /// \param data is the function being analyzed
        private static void funcLinkOutput(FuncCallSpecs fc, Funcdata data)
        {
            PcodeOp callop = fc.getOp();
            if (callop.getOut() != (Varnode)null) {
                // CALL ops are expected to have no output, but its possible an override has produced one
                if (callop.getOut().getSpace().getType() == spacetype.IPTR_INTERNAL) {
                    // Removing a varnode in the unique space will likely produce an input varnode in the unique space
                    TextWriter s = new StringWriter();
                    s.Write("CALL op at ");
                    callop.getAddr().printRaw(s);
                    s.Write(" has an unexpected output varnode");
                    throw new LowlevelError(s.ToString());
                }
                // Otherwise just remove the Varnode and assume return recovery will reintroduce it if necessary
                data.opUnsetOutput(callop);
            }
            if (fc.isOutputLocked()) {
                ProtoParameter outparam = fc.getOutput();
                Datatype outtype = outparam.getType();
                if (outtype.getMetatype() != type_metatype.TYPE_VOID) {
                    int sz = outparam.getSize();
                    if (sz == 1 && outtype.getMetatype() == type_metatype.TYPE_BOOL
                        && data.isTypeRecoveryOn())
                    {
                        data.opMarkCalculatedBool(callop);
                    }
                    Address addr = outparam.getAddress();
                    data.newVarnodeOut(sz, addr, callop);
                    VarnodeData vdata;
                    OpCode res = fc.assumedOutputExtension(addr, sz, vdata);
                    if (res == OpCode.CPUI_PIECE) {
                        // Pick an extension based on type
                        if (outtype.getMetatype() == type_metatype.TYPE_INT)
                            res = OpCode.CPUI_INT_SEXT;
                        else
                            res = OpCode.CPUI_INT_ZEXT;
                    }
                    if (res != OpCode.CPUI_COPY) {
                        // We assume the (smallsize) output is extended to a full register
                        // Create the extension operation to eliminate artifact
                        PcodeOp op = data.newOp(1, callop.getAddr());
                        data.newVarnodeOut(vdata.size, vdata.getAddr(), op);
                        Varnode invn = data.newVarnode(sz, addr);
                        data.opSetInput(op, invn, 0);
                        data.opSetOpcode(op, res);
                        data.opInsertAfter(op, callop); // Insert immediately after the call
                    }
                }
            }
            else
                fc.initActiveOutput();
        }

        public ActionFuncLink(string g)
            : base(ruleflags.rule_onceperfunc,"funclink", g)
        {
        }

        public override Action? clone(ActionGroupList grouplist)
        {
            return (!grouplist.contains(getGroup())) ? null : new ActionFuncLink(getGroup());
        }

        public override int apply(Funcdata data)
        {
            int i, size;

            size = data.numCalls();
            for (i = 0; i < size; ++i) {
                funcLinkInput(data.getCallSpecs(i), data);
                funcLinkOutput(data.getCallSpecs(i), data);
            }
            return 0;
        }
    }
}
