using Sla.DECCORE;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    internal class IfcPrintExtrapop : IfaceDecompCommand
    {
        /// \class IfcPrintExtrapop
        /// \brief Print change to stack pointer for called function: `print extrapop [<functionname>]`
        ///
        /// For the selected function, the extra amount each called function changes the stack pointer
        /// (over popping the return value) is printed to console.  The function is selected by
        /// name, or if no name is given, the \e current function is selected.
        public override void execute(TextReader s)
        {
            string name;

            s >> ws >> name;
            if (name.size() == 0)
            {
                if (dcp.fd != (Funcdata)null)
                {
                    int num = dcp.fd.numCalls();
                    for (int i = 0; i < num; ++i)
                    {
                        FuncCallSpecs fc = dcp.fd.getCallSpecs(i);
                        *status.optr << "ExtraPop for " << fc.getName() << '(';
                        *status.optr << fc.getOp().getAddr() << ')';
                        int expop = fc.getEffectiveExtraPop();
                        *status.optr << " ";
                        if (expop == ProtoModel.extrapop_unknown)
                            *status.optr << "unknown";
                        else
                            *status.optr << dec << expop;
                        *status.optr << '(';
                        expop = fc.getExtraPop();
                        if (expop == ProtoModel.extrapop_unknown)
                            *status.optr << "unknown";
                        else
                            *status.optr << dec << expop;
                        *status.optr << ')' << endl;
                    }
                }
                else
                {
                    int expop = dcp.conf.defaultfp.getExtraPop();
                    *status.optr << "Default extra pop = ";
                    if (expop == ProtoModel.extrapop_unknown)
                        *status.optr << "unknown" << endl;
                    else
                        *status.optr << dec << expop << endl;
                }
            }
            else
            {
                Funcdata* fd;
                fd = dcp.conf.symboltab.getGlobalScope().queryFunction(name);
                if (fd == (Funcdata)null)
                    throw new IfaceExecutionError("Unknown function: " + name);
                int expop = fd.getFuncProto().getExtraPop();
                *status.optr << "ExtraPop for function " << name << " is ";
                if (expop == ProtoModel.extrapop_unknown)
                    *status.optr << "unknown" << endl;
                else
                    *status.optr << dec << expop << endl;
                if (dcp.fd != (Funcdata)null)
                {
                    int num = dcp.fd.numCalls();
                    for (int i = 0; i < num; ++i)
                    {
                        FuncCallSpecs fc = dcp.fd.getCallSpecs(i);
                        if (fc.getName() == fd.getName())
                        {
                            expop = fc.getEffectiveExtraPop();
                            *status.optr << "For this function, extrapop = ";
                            if (expop == ProtoModel.extrapop_unknown)
                                *status.optr << "unknown";
                            else
                                *status.optr << dec << expop;
                            *status.optr << '(';
                            expop = fc.getExtraPop();
                            if (expop == ProtoModel.extrapop_unknown)
                                *status.optr << "unknown";
                            else
                                *status.optr << dec << expop;
                            *status.optr << ')' << endl;
                        }
                    }
                }
            }
        }
    }
}
