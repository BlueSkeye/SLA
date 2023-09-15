using Sla.CORE;
using Sla.DECCORE;

namespace Sla.EXTRA
{
    internal class IfcAnalyzeRange : IfaceDecompCommand
    {
        /// \class IfcAnalyzeRange
        /// \brief Run value-set analysis on the \e current function: `analyze range full|partial <varnode>`
        ///
        /// The analysis targets a single varnode as specified on the command-line and is based on
        /// the existing data-flow graph for the current function.
        /// The possible values that can reach the varnode at its point of definition, and
        /// at any point it is involved in a LOAD or STORE, are displayed.
        /// The keywords \b full and \b partial choose whether the value-set analysis uses
        /// full or partial widening.
        public override void execute(TextReader s)
        {
            if (dcp.conf == (Architecture)null)
                throw new IfaceExecutionError("Image not loaded");
            if (dcp.fd == (Funcdata)null)
                throw new IfaceExecutionError("No function selected");

            bool useFullWidener;
            s.ReadSpaces();
            string token = s.ReadString();
            if (token == "full")
                useFullWidener = true;
            else if (token == "partial") {
                useFullWidener = false;
            }
            else
                throw new IfaceParseError("Must specify \"full\" or \"partial\" widening");
            Varnode vn = dcp.readVarnode(s);
            List<Varnode> sinks = new List<Varnode>();
            List<PcodeOp> reads = new List<PcodeOp>();
            sinks.Add(vn);
            IEnumerator<PcodeOp> opEnumerator = vn.beginDescend();
            while (opEnumerator.MoveNext()) {
                PcodeOp op = opEnumerator.Current;
                if (op.code() == OpCode.CPUI_LOAD || op.code() == OpCode.CPUI_STORE)
                    reads.Add(op);
            }
            Varnode stackReg = dcp.fd.findSpacebaseInput(dcp.conf.getStackSpace());
            ValueSetSolver vsSolver = new ValueSetSolver();
            vsSolver.establishValueSets(sinks, reads, stackReg, false);
            if (useFullWidener) {
                vsSolver.solve(10000, new WidenerFull());
            }
            else {
                vsSolver.solve(10000, new WidenerNone());
            }
            IEnumerator<ValueSet> valueSetEnumerator = vsSolver.beginValueSets();
            while (valueSetEnumerator.MoveNext()) {
                valueSetEnumerator.Current.printRaw(status.optr);
                status.optr.WriteLine();
            }
            IEnumerator<KeyValuePair<SeqNum, ValueSetRead>> riter = vsSolver.beginValueSetReads();
            while (riter.MoveNext()) {
                riter.Current.Value.printRaw(status.optr);
                status.optr.WriteLine();
            }
        }
    }
}
