using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    internal class ParamIDAnalysis
    {
        private Funcdata fd;
        private List<ParamMeasure> InputParamMeasures;
        private List<ParamMeasure> OutputParamMeasures;
        
        public ParamIDAnalysis(Funcdata fd_in, bool justproto)
        {
            fd = fd_in;
            if (justproto)
            {       // We only provide info on the recovered prototype
                FuncProto fproto = fd.getFuncProto();
                int num = fproto.numParams();
                for (int i = 0; i < num; ++i)
                {
                    ProtoParameter* param = fproto.getParam(i);
                    InputParamMeasures.Add(ParamMeasure(param.getAddress(), param.getSize(),
                                       param.getType(), ParamMeasure::INPUT));
                    Varnode* vn = fd.findVarnodeInput(param.getSize(), param.getAddress());
                    if (vn != (Varnode)null)
                        InputParamMeasures.GetLastItem().calculateRank(true, vn, (PcodeOp)null);
                }

                ProtoParameter* outparam = fproto.getOutput();
                if (!outparam.getAddress().isInvalid())
                { // If we don't have a void type
                    OutputParamMeasures.Add(ParamMeasure(outparam.getAddress(), outparam.getSize(),
                                         outparam.getType(), ParamMeasure::OUTPUT));
                    list<PcodeOp*>::const_iterator rtn_iter = fd.beginOp(CPUI_RETURN);
                    while (rtn_iter != fd.endOp(CPUI_RETURN))
                    {
                        PcodeOp* rtn_op = *rtn_iter;
                        // For RETURN op, input0 is address location of indirect return, input1,
                        // if it exists, is the Varnode returned, output = not sure.
                        if (rtn_op.numInput() == 2)
                        {
                            Varnode* ovn = rtn_op.getIn(1);
                            if (ovn != (Varnode)null)
                            {  //Not a void return
                                OutputParamMeasures.GetLastItem().calculateRank(true, ovn, rtn_op);
                                break;
                            }
                        }
                        rtn_iter++;
                    }
                }
            }
            else
            {
                // Need to list input varnodes that are outside of the model
                VarnodeDefSet::const_iterator iter, enditer;
                iter = fd.beginDef(Varnode.varnode_flags.input);
                enditer = fd.endDef(Varnode.varnode_flags.input);
                while (iter != enditer)
                {
                    Varnode* invn = *iter;
                    ++iter;
                    InputParamMeasures.Add(ParamMeasure(invn.getAddr(), invn.getSize(),
                                       invn.getType(), ParamMeasure::INPUT));
                    InputParamMeasures.GetLastItem().calculateRank(true, invn, (PcodeOp)null);
                }
            }
        }

        public void encode(Encoder encoder, bool moredetail)
        {
            encoder.openElement(ELEM_PARAMMEASURES);
            encoder.writeString(ATTRIB_NAME, fd.getName());
            fd.getAddress().encode(encoder);
            encoder.openElement(ELEM_PROTO);

            encoder.writeString(ATTRIB_MODEL, fd.getFuncProto().getModelName());
            int extrapop = fd.getFuncProto().getExtraPop();
            if (extrapop == ProtoModel::extrapop_unknown)
                encoder.writeString(ATTRIB_EXTRAPOP, "unknown");
            else
                encoder.writeSignedInteger(ATTRIB_EXTRAPOP, extrapop);
            encoder.closeElement(ELEM_PROTO);
            list<ParamMeasure>::const_iterator pm_iter;
            for (pm_iter = InputParamMeasures.begin(); pm_iter != InputParamMeasures.end(); ++pm_iter)
            {
                ParamMeasure pm = *pm_iter;
                pm.encode(encoder, ELEM_INPUT, moredetail);
            }
            for (pm_iter = OutputParamMeasures.begin(); pm_iter != OutputParamMeasures.end(); ++pm_iter)
            {
                ParamMeasure pm = *pm_iter;
                pm.encode(encoder, ELEM_OUTPUT, moredetail);
            }
            encoder.closeElement(ELEM_PARAMMEASURES);
        }

        public void savePretty(TextWriter s, bool moredetail)
        {
            s << "Param Measures\nFunction: " << fd.getName() << "\nAddress: 0x" << hex << fd.getAddress().getOffset() << "\n";
            s << "Model: " << fd.getFuncProto().getModelName() << "\nExtrapop: " << fd.getFuncProto().getExtraPop() << "\n";
            s << "Num Params: " << InputParamMeasures.size() << "\n";
            list<ParamMeasure>::const_iterator pm_iter = InputParamMeasures.begin();
            for (pm_iter = InputParamMeasures.begin(); pm_iter != InputParamMeasures.end(); ++pm_iter)
            {
                ParamMeasure pm = *pm_iter;
                pm.savePretty(s, moredetail);
            }
            s << "Num Returns: " << OutputParamMeasures.size() << "\n";
            pm_iter = OutputParamMeasures.begin();
            for (pm_iter = OutputParamMeasures.begin(); pm_iter != OutputParamMeasures.end(); ++pm_iter)
            {
                ParamMeasure pm = *pm_iter;
                pm.savePretty(s, moredetail);
            }
            s << "\n";
        }
    }
}
