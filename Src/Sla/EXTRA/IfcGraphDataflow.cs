using Sla.DECCORE;
using static System.Runtime.InteropServices.JavaScript.JSType;
using System.IO;

namespace Sla.EXTRA
{
    internal class IfcGraphDataflow : IfaceDecompCommand
    {
        /// \class IfcGraphDataflow
        /// \brief Write a graph representation of data-flow to a file: `graph dataflow <filename>`
        ///
        /// The data-flow graph for the \e current function, in its current state of transform,
        /// is written to the indicated file.
        public override void execute(TextReader s)
        {
            string filename;

            if (dcp.fd == (Funcdata)null)
                throw new IfaceExecutionError("No function selected");

            s >> filename;
            if (filename.Length == 0)
                throw new IfaceParseError("Missing output file");
            if (!dcp.fd.isProcStarted())
                throw new IfaceExecutionError("Syntax tree not calculated");
            StreamWriter thefile;
            try { thefile = new StreamWriter(File.OpenWrite(filename)); }
            catch {
                throw new IfaceExecutionError($"Unable to open output file: {filename}");
            }
            dump_dataflow_graph(dcp.fd, thefile);
            thefile.Close();
        }
        private static void dump_dataflow_graph(Funcdata data, TextWriter s)
        {
            s.WriteLine($"*CMD=NewGraphWindow, WindowName={data.getName()}-dataflow;");
            s.WriteLine($"*CMD=*NEXUS,Name={data.getName()}-dataflow;");

            s.WriteLine("\n// AutomaticArrangement");
            s.WriteLine("  *CMD = AlterLocalPreferences, Name = AutomaticArrangement,");
            s.WriteLine("  ~ReplaceAllParams = TRUE,");
            s.WriteLine("  EnableAutomaticArrangement=true,");
            s.WriteLine("  OnlyActOnVerticesWithoutCoordsIfOff=false,");
            s.WriteLine("  DontUpdateMediumWithUserArrangement=false,");
            s.WriteLine("  UserAddedArrangmentParams=({ServiceName=SimpleHierarchyFromSources,ServiceParams={~SkipPromptForParams=true}}),");
            s.WriteLine("  SmallSize=50,");
            s.WriteLine("  DontUpdateLargeWithUserArrangement=true,");
            s.WriteLine("  NewVertexActionIfOff=ArrangeByMDS,");
            s.WriteLine("  MediumSizeArrangement=SimpleHierarchyFromSources,");
            s.WriteLine("  SmallSizeArrangement=SimpleHierarchyFromSources,");
            s.WriteLine("  MediumSize=800,");
            s.WriteLine("  LargeSizeArrangement=ArrangeInCircle,");
            s.WriteLine("  DontUpdateSmallWithUserArrangement=false,");
            s.WriteLine("  ActionSizeGainIfOff=1.0;");

            s.WriteLine("\n// VertexColors");
            s.WriteLine("  *CMD = AlterLocalPreferences, Name = VertexColors,");
            s.WriteLine("  ~ReplaceAllParams = TRUE,");
            s.WriteLine("  Mapping=({DisplayChoice=Magenta,AttributeValue=branch},");
            s.WriteLine("  {DisplayChoice=Blue,AttributeValue=register},");
            s.WriteLine("  {DisplayChoice=Black,AttributeValue=unique},");
            s.WriteLine("  {DisplayChoice=DarkGreen,AttributeValue=const},");
            s.WriteLine("  {DisplayChoice=DarkOrange,AttributeValue=ram},");
            s.WriteLine("  {DisplayChoice=Orange,AttributeValue=stack}),");
            s.WriteLine("  ChoiceForValueNotCovered=Red,");
            s.WriteLine("  Extraction=CompleteValue,");
            s.WriteLine("  ExtractionParams={},");
            s.WriteLine("  AttributeName=SubClass,");
            s.WriteLine("  ChoiceForMissingValue=Red,");
            s.WriteLine("  CanOverride=true,");
            s.WriteLine("  OverrideAttributeName=Color,");
            s.WriteLine("  UsingRange=false;");

            s.WriteLine("\n//     VertexIcons");
            s.WriteLine("  *CMD = AlterLocalPreferences, Name = VertexIcons,");
            s.WriteLine("  ~ReplaceAllParams = TRUE,");
            s.WriteLine("  Mapping=({DisplayChoice=Circle,AttributeValue=var},");
            s.WriteLine("  {DisplayChoice=Square,AttributeValue=op}),");
            s.WriteLine("  ChoiceForValueNotCovered=Circle,");
            s.WriteLine("  Extraction=CompleteValue,");
            s.WriteLine("  ExtractionParams={},");
            s.WriteLine("  AttributeName=Type,");
            s.WriteLine("  ChoiceForMissingValue=Circle,");
            s.WriteLine("  CanOverride=true,");
            s.WriteLine("  OverrideAttributeName=Icon,");
            s.WriteLine("  UsingRange=false;");

            s.WriteLine("\n//     VertexLabels");
            s.WriteLine("  *CMD = AlterLocalPreferences, Name = VertexLabels,");
            s.WriteLine("  ~ReplaceAllParams = TRUE,");
            s.WriteLine("  Center=({SpecialColor=Black,SpecialFontName=SansSerif,Format=StandardFormat,UseSpecialFontName=false,LabelAlignment=Center,TreatBackSlashNAsNewLine=false,MaxLines=4,FontSize=10,IncludeBackground=false,SqueezeLinesTogether=true,BackgroundColor=Black,UseSpecialColor=false,AttributeName=Name,MaxWidth=100}),");
            s.WriteLine("  East=(),");
            s.WriteLine("  SouthEast=(),");
            s.WriteLine("  North=(),");
            s.WriteLine("  West=(),");
            s.WriteLine("  SouthWest=(),");
            s.WriteLine("  NorthEast=(),");
            s.WriteLine("  South=(),");
            s.WriteLine("  NorthWest=();");

            s.WriteLine("\n// Attributes");
            s.WriteLine("*CMD=DefineAttribute,");
            s.WriteLine("        Name=SubClass,");
            s.WriteLine("        Type=String,");
            s.WriteLine("        Category=Vertices;\n");
            s.WriteLine("*CMD=DefineAttribute,");
            s.WriteLine("        Name=Type,");
            s.WriteLine("        Type=String,");
            s.WriteLine("        Category=Vertices;\n");
            s.WriteLine("*CMD=DefineAttribute,");
            s.WriteLine("        Name=Internal,");
            s.WriteLine("        Type=String,");
            s.WriteLine("        Category=Vertices;\n");
            s.WriteLine("*CMD=DefineAttribute,");
            s.WriteLine("        Name=Name,");
            s.WriteLine("        Type=String,");
            s.WriteLine("        Category=Vertices;\n");
            s.WriteLine("*CMD=DefineAttribute,");
            s.WriteLine("        Name=Address,");
            s.WriteLine("        Type=String,");
            s.WriteLine("        Category=Vertices;\n");

            s.WriteLine("*CMD=DefineAttribute,");
            s.WriteLine("        Name=Name,");
            s.WriteLine("        Type=String,");
            s.WriteLine("        Category=Edges;\n");

            s.WriteLine("*CMD=SetKeyAttribute,");
            s.WriteLine("        Category=Vertices,");
            s.WriteLine("        Name=Internal;\n");
            Graph.dump_varnode_vertex(data, s);
            Graph.dump_op_vertex(data, s);
            Graph.dump_edges(data, s);
        }
    }
}
