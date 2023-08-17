using Sla.CORE;

namespace Sla.DECCORE
{
    internal class ParamIDAnalysis
    {
        private Funcdata fd;
        private List<ParamMeasure> InputParamMeasures = new List<ParamMeasure>();
        private List<ParamMeasure> OutputParamMeasures = new List<ParamMeasure>();
        
        public ParamIDAnalysis(Funcdata fd_in, bool justproto)
        {
            fd = fd_in;
            if (justproto) {
                // We only provide info on the recovered prototype
                FuncProto fproto = fd.getFuncProto();
                int num = fproto.numParams();
                for (int i = 0; i < num; ++i) {
                    ProtoParameter param = fproto.getParam(i);
                    InputParamMeasures.Add(new ParamMeasure(param.getAddress(), param.getSize(),
                        param.getType(), ParamMeasure.ParamIDIO.INPUT));
                    Varnode vn = fd.findVarnodeInput(param.getSize(), param.getAddress());
                    if (vn != (Varnode)null)
                        InputParamMeasures.GetLastItem().calculateRank(true, vn, (PcodeOp)null);
                }

                ProtoParameter outparam = fproto.getOutput();
                if (!outparam.getAddress().isInvalid()) {
                    // If we don't have a void type
                    OutputParamMeasures.Add(new ParamMeasure(outparam.getAddress(), outparam.getSize(),
                        outparam.getType(), ParamMeasure.ParamIDIO.OUTPUT));
                    IEnumerator<PcodeOp> rtn_iter = fd.beginOp(OpCode.CPUI_RETURN);
                    while (rtn_iter != fd.endOp(OpCode.CPUI_RETURN)) {
                        PcodeOp rtn_op = rtn_iter.Current;
                        // For RETURN op, input0 is address location of indirect return, input1,
                        // if it exists, is the Varnode returned, output = not sure.
                        if (rtn_op.numInput() == 2) {
                            Varnode? ovn = rtn_op.getIn(1);
                            if (ovn != (Varnode)null) {
                                //Not a void return
                                OutputParamMeasures.GetLastItem().calculateRank(true, ovn, rtn_op);
                                break;
                            }
                        }
                        rtn_iter++;
                    }
                }
            }
            else {
                // Need to list input varnodes that are outside of the model
                VarnodeDefSet::const_iterator iter, enditer;
                iter = fd.beginDef(Varnode.varnode_flags.input);
                enditer = fd.endDef(Varnode.varnode_flags.input);
                while (iter != enditer) {
                    Varnode invn = iter.Current;
                    ++iter;
                    InputParamMeasures.Add(new ParamMeasure(invn.getAddr(), invn.getSize(),
                        invn.getType(), ParamMeasure.ParamIDIO.INPUT));
                    InputParamMeasures.GetLastItem().calculateRank(true, invn, (PcodeOp)null);
                }
            }
        }

        public void encode(Sla.CORE.Encoder encoder, bool moredetail)
        {
            encoder.openElement(ElementId.ELEM_PARAMMEASURES);
            encoder.writeString(AttributeId.ATTRIB_NAME, fd.getName());
            fd.getAddress().encode(encoder);
            encoder.openElement(ElementId.ELEM_PROTO);

            encoder.writeString(AttributeId.ATTRIB_MODEL, fd.getFuncProto().getModelName());
            int extrapop = fd.getFuncProto().getExtraPop();
            if (extrapop == ProtoModel.extrapop_unknown)
                encoder.writeString(AttributeId.ATTRIB_EXTRAPOP, "unknown");
            else
                encoder.writeSignedInteger(AttributeId.ATTRIB_EXTRAPOP, extrapop);
            encoder.closeElement(ElementId.ELEM_PROTO);
            foreach (ParamMeasure pm in InputParamMeasures) {
                pm.encode(encoder, ElementId.ELEM_INPUT, moredetail);
            }
            foreach (ParamMeasure pm in OutputParamMeasures) {
                pm.encode(encoder, ElementId.ELEM_OUTPUT, moredetail);
            }
            encoder.closeElement(ElementId.ELEM_PARAMMEASURES);
        }

        public void savePretty(TextWriter s, bool moredetail)
        {
            s.WriteLine($"Param Measures");
            s.WriteLine($"Function: {fd.getName()}");
            s.WriteLine($"Address: 0x{fd.getAddress().getOffset():X}");
            s.WriteLine($"Model: {fd.getFuncProto().getModelName()}");
            s.WriteLine($"Extrapop: {fd.getFuncProto().getExtraPop()}");
            s.WriteLine($"Num Params: {InputParamMeasures.size()}");
            foreach (ParamMeasure pm in InputParamMeasures) {
                pm.savePretty(s, moredetail);
            }
            s.WriteLine("Num Returns: {OutputParamMeasures.size()}");
            foreach (ParamMeasure pm in OutputParamMeasures) {
                pm.savePretty(s, moredetail);
            }
            s.WriteLine();
        }
    }
}
