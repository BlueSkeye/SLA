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

            string infile = s.ReadString();
            string outfile = s.ReadString();

            if (infile.empty())
                throw new IfaceParseError("Missing input file");
            if (outfile.empty())
                throw new IfaceParseError("Missing output file");

            TextReader fs;
            try { fs = new StreamReader(File.OpenRead(infile)); }
            catch {
                throw new IfaceExecutionError($"Unable to open file: {infile}");
            }

            DocumentStorage store = new DocumentStorage();
            Document doc = store.parseDocument(fs);
            fs.Close();

            try {
                BlockGraph ingraph = new BlockGraph();
                XmlDecode decoder = new XmlDecode(dcp.conf, doc.getRoot());
                ingraph.decode(decoder);

                BlockGraph resultgraph = new BlockGraph();
                List<FlowBlock> rootlist = new List<FlowBlock>();

                resultgraph.buildCopy(ingraph);
                resultgraph.structureLoops(rootlist);
                resultgraph.calcForwardDominator(rootlist);

                CollapseStructure collapse = new CollapseStructure(resultgraph);
                collapse.collapseAll();

                TextWriter sout;
                try { sout = new StreamWriter(File.OpenWrite(outfile)); }
                catch {
                    throw new IfaceExecutionError($"Unable to open output file: {outfile}");
                }
                XmlEncode encoder = new XmlEncode(sout);
                resultgraph.encode(encoder);
                sout.Close();
            }
            catch (CORE.LowlevelError err) {
                status.optr.WriteLine(err.ToString());
            }
        }
    }
}
