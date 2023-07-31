using Sla.CORE;
using Sla.DECCORE;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.Intrinsics.Arm;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    internal class IfcStructureBlocks : IfaceDecompCommand
    {
        /// \class IfcStructureBlocks
        /// \brief Structure an external control-flow graph: `structure blocks <infile> <outfile>`
        ///
        /// The control-flow graph is read in from XML file, structuring is performed, and the
        /// result is written out to a separate XML file.
        public override void execute(TextReader s)
        {
            if (dcp.conf == (Architecture)null)
                throw new IfaceExecutionError("No load image present");

            string infile, outfile;
            s >> infile;
            s >> outfile;

            if (infile.empty())
                throw new IfaceParseError("Missing input file");
            if (outfile.empty())
                throw new IfaceParseError("Missing output file");

            ifstream fs;
            fs.open(infile.c_str());
            if (!fs)
                throw new IfaceExecutionError("Unable to open file: " + infile);

            DocumentStorage store;
            Document* doc = store.parseDocument(fs);
            fs.close();

            try
            {
                BlockGraph ingraph;
                XmlDecode decoder(dcp.conf, doc.getRoot());
                ingraph.decode(decoder);

                BlockGraph resultgraph;
                List<FlowBlock*> rootlist;

                resultgraph.buildCopy(ingraph);
                resultgraph.structureLoops(rootlist);
                resultgraph.calcForwardDominator(rootlist);

                CollapseStructure collapse(resultgraph);
                collapse.collapseAll();

                ofstream sout;
                sout.open(outfile.c_str());
                if (!sout)
                    throw new IfaceExecutionError("Unable to open output file: " + outfile);
                XmlEncode encoder(sout);
                resultgraph.encode(encoder);
                sout.close();
            }
            catch (LowlevelError err) {
                *status.optr << err.ToString() << endl;
            }
        }
    }
}
