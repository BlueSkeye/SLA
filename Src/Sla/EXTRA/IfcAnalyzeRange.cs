using Sla.CORE;
using Sla.DECCORE;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

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
            if (dcp.conf == (Architecture*)0)
                throw IfaceExecutionError("Image not loaded");
            if (dcp.fd == (Funcdata*)0)
                throw IfaceExecutionError("No function selected");

            bool useFullWidener;
            string token;
            s >> ws >> token;
            if (token == "full")
                useFullWidener = true;
            else if (token == "partial")
            {
                useFullWidener = false;
            }
            else
                throw IfaceParseError("Must specify \"full\" or \"partial\" widening");
            Varnode* vn = dcp.readVarnode(s);
            List<Varnode*> sinks;
            List<PcodeOp*> reads;
            sinks.push_back(vn);
            for (list<PcodeOp*>::const_iterator iter = vn.beginDescend(); iter != vn.endDescend(); ++iter)
            {
                PcodeOp* op = *iter;
                if (op.code() == CPUI_LOAD || op.code() == CPUI_STORE)
                    reads.push_back(op);
            }
            Varnode* stackReg = dcp.fd.findSpacebaseInput(dcp.conf.getStackSpace());
            ValueSetSolver vsSolver;
            vsSolver.establishValueSets(sinks, reads, stackReg, false);
            if (useFullWidener)
            {
                WidenerFull widener;
                vsSolver.solve(10000, widener);
            }
            else
            {
                WidenerNone widener;
                vsSolver.solve(10000, widener);
            }
            list<ValueSet>::const_iterator iter;
            for (iter = vsSolver.beginValueSets(); iter != vsSolver.endValueSets(); ++iter)
            {
                (*iter).printRaw(*status.optr);
                *status.optr << endl;
            }
            map<SeqNum, ValueSetRead>::const_iterator riter;
            for (riter = vsSolver.beginValueSetReads(); riter != vsSolver.endValueSetReads(); ++riter)
            {
                (*riter).second.printRaw(*status.optr);
                *status.optr << endl;
            }
        }
    }
}
