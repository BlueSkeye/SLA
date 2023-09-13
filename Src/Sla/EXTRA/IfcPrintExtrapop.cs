using Sla.DECCORE;

namespace Sla.EXTRA
{
    internal class IfcPrintExtrapop : IfaceDecompCommand
    {
        /// \class IfcPrintExtrapop
        /// \brief Print change to stack pointer for called function: `print extrapop [<functionname>]`
        /// For the selected function, the extra amount each called function changes the stack pointer
        /// (over popping the return value) is printed to console.  The function is selected by
        /// name, or if no name is given, the \e current function is selected.
        public override void execute(TextReader s)
        {
            s.ReadSpaces();
            string name = s.ReadString();
            if (name.Length == 0) {
                if (dcp.fd != (Funcdata)null) {
                    int num = dcp.fd.numCalls();
                    for (int i = 0; i < num; ++i) {
                        FuncCallSpecs fc = dcp.fd.getCallSpecs(i);
                        status.optr.Write($"ExtraPop for {fc.getName()}({fc.getOp().getAddr()})");
                        int expop = fc.getEffectiveExtraPop();
                        status.optr.Write(" ");
                        if (expop == ProtoModel.extrapop_unknown)
                            status.optr.Write("unknown");
                        else
                            status.optr.Write(expop);
                        status.optr.Write("(");
                        expop = fc.getExtraPop();
                        if (expop == ProtoModel.extrapop_unknown)
                            status.optr.Write("unknown");
                        else
                            status.optr.Write(expop);
                        status.optr.WriteLine(')');
                    }
                }
                else {
                    int expop = dcp.conf.defaultfp.getExtraPop();
                    status.optr.Write("Default extra pop = ");
                    if (expop == ProtoModel.extrapop_unknown)
                        status.optr.WriteLine("unknown");
                    else
                        status.optr.WriteLine(expop);
                }
            }
            else {
                Funcdata? fd = dcp.conf.symboltab.getGlobalScope().queryFunction(name);
                if (fd == (Funcdata)null)
                    throw new IfaceExecutionError("Unknown function: " + name);
                int expop = fd.getFuncProto().getExtraPop();
                status.optr.Write($"ExtraPop for function {name} is ");
                if (expop == ProtoModel.extrapop_unknown)
                    status.optr.WriteLine("unknown");
                else
                    status.optr.WriteLine(expop);
                if (dcp.fd != (Funcdata)null) {
                    int num = dcp.fd.numCalls();
                    for (int i = 0; i < num; ++i) {
                        FuncCallSpecs fc = dcp.fd.getCallSpecs(i);
                        if (fc.getName() == fd.getName()) {
                            expop = fc.getEffectiveExtraPop();
                            status.optr.Write("For this function, extrapop = ");
                            if (expop == ProtoModel.extrapop_unknown)
                                status.optr.Write("unknown");
                            else
                                status.optr.Write(expop);
                            status.optr.Write('(');
                            expop = fc.getExtraPop();
                            if (expop == ProtoModel.extrapop_unknown)
                                status.optr.Write("unknown");
                            else
                                status.optr.Write(expop);
                            status.optr.WriteLine(')');
                        }
                    }
                }
            }
        }
    }
}
