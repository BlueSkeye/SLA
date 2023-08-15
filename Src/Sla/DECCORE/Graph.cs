using Sla.CORE;

namespace Sla.DECCORE
{
    internal static class Graph
    {
        internal static void dump_block_edges(BlockGraph graph, TextWriter s)
        {
            s.WriteLine("\n\n// Add Edges");
            s.WriteLine("*CMD=*COLUMNAR_INPUT,");
            s.WriteLine("  Command=AddEdges,");
            s.WriteLine("  Parsing=WhiteSpace,");
            s.WriteLine("  Fields=({Name=*FromKey, Location=1},");
            s.WriteLine("          {Name=*ToKey, Location=2});\n");

            for (int i = 0; i < graph.getSize(); ++i)
                print_block_edge(graph.getBlock(i), s);
            s.WriteLine("*END_COLUMNS");
        }

        internal static void dump_dom_graph(string name, BlockGraph graph, TextWriter s)
        {
            int count = 0;

            for (int i = 0; i < graph.getSize(); ++i)
                if (graph.getBlock(i).getImmedDom() == (FlowBlock)null)
                    count += 1;
            bool falsenode = (count > 1);
            s.WriteLine($"*CMD=NewGraphWindow, WindowName={name}-dom;");
            s.WriteLine($"*CMD=*NEXUS,Name={name}-dom;");
            dump_block_properties(s);
            dump_block_attributes(s);
            dump_block_vertex(graph, s, falsenode);
            dump_dom_edges(graph, s, falsenode);
        }

        internal static void dump_block_vertex(BlockGraph graph, TextWriter s, bool falsenode)
        {
            s.WriteLine("\n\n// Add Vertices");
            s.WriteLine("*CMD=*COLUMNAR_INPUT,");
            s.WriteLine("  Command=AddVertices,");
            s.WriteLine("  Parsing=WhiteSpace,");
            s.WriteLine("  Fields=({Name=SizeOut, Location=1},");
            s.WriteLine("          {Name=SizeIn, Location=2},");
            s.WriteLine("          {Name=Internal, Location=3},");
            s.WriteLine("          {Name=Index, Location=4},");
            s.WriteLine("          {Name=Start, Location=5},");
            s.WriteLine("          {Name=Stop, Location=6});\n");

            if (falsenode)
                s.WriteLine("-1 0 0 -1 0 0");
            for (int i = 0; i < graph.getSize(); ++i)
                print_block_vertex(graph.getBlock(i), s);
            s.WriteLine("*END_COLUMNS");
        }

        internal static void dump_block_attributes(TextWriter s)
        {
            s.WriteLine("\n// Attributes");
            s.WriteLine("*CMD=DefineAttribute,");
            s.WriteLine("        Name=SizeOut,");
            s.WriteLine("        Type=String,");
            s.WriteLine("        Category=Vertices;\n");
            s.WriteLine("*CMD=DefineAttribute,");
            s.WriteLine("        Name=SizeIn,");
            s.WriteLine("        Type=String,");
            s.WriteLine("        Category=Vertices;\n");
            s.WriteLine("*CMD=DefineAttribute,");
            s.WriteLine("        Name=Internal,");
            s.WriteLine("        Type=String,");
            s.WriteLine("        Category=Vertices;\n");
            s.WriteLine("*CMD=DefineAttribute,");
            s.WriteLine("        Name=Index,");
            s.WriteLine("        Type=String,");
            s.WriteLine("        Category=Vertices;\n");
            s.WriteLine("*CMD=DefineAttribute,");
            s.WriteLine("        Name=Start,");
            s.WriteLine("        Type=String,");
            s.WriteLine("        Category=Vertices;\n");
            s.WriteLine("*CMD=DefineAttribute,");
            s.WriteLine("        Name=Stop,");
            s.WriteLine("        Type=String,");
            s.WriteLine("        Category=Vertices;\n");

            s.WriteLine("*CMD=SetKeyAttribute,");
            s.WriteLine("        Category=Vertices,");
            s.WriteLine("        Name=Index;\n");
        }

        internal static void dump_block_properties(TextWriter s)
        {
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
            s.WriteLine("  Mapping=({DisplayChoice=Red,AttributeValue=0},");
            s.WriteLine("  {DisplayChoice=Blue,AttributeValue=1},");
            s.WriteLine("  {DisplayChoice=Yellow,AttributeValue=2}),");
            s.WriteLine("  ChoiceForValueNotCovered=Purple,");
            s.WriteLine("  Extraction=CompleteValue,");
            s.WriteLine("  ExtractionParams={},");
            s.WriteLine("  AttributeName=SizeOut,");
            s.WriteLine("  ChoiceForMissingValue=Purple,");
            s.WriteLine("  CanOverride=true,");
            s.WriteLine("  OverrideAttributeName=Color,");
            s.WriteLine("  UsingRange=false;");

            s.WriteLine("\n//     VertexIcons");
            s.WriteLine("  *CMD = AlterLocalPreferences, Name = VertexIcons,");
            s.WriteLine("  ~ReplaceAllParams = TRUE,");
            s.WriteLine("  Mapping=({DisplayChoice = Square,AttributeValue = 0}),");
            s.WriteLine("  ChoiceForValueNotCovered=Circle,");
            s.WriteLine("  Extraction=CompleteValue,");
            s.WriteLine("  ExtractionParams={},");
            s.WriteLine("  AttributeName=SizeIn,");
            s.WriteLine("  ChoiceForMissingValue=Circle,");
            s.WriteLine("  CanOverride=true,");
            s.WriteLine("  OverrideAttributeName=Icon,");
            s.WriteLine("  UsingRange=false;");

            s.WriteLine("\n//     VertexLabels");
            s.WriteLine("  *CMD = AlterLocalPreferences, Name = VertexLabels,");
            s.WriteLine("  ~ReplaceAllParams = TRUE,");
            s.WriteLine("  Center=({MaxLines = 4,SqueezeLinesTogether = true,TreatBackSlashNAsNewLine=false,FontSize=10,Format=StandardFormat,IncludeBackground=false,BackgroundColor=Black,AttributeName=Start,UseSpecialFontName=false,SpecialColor=Black,SpecialFontName=SansSerif,UseSpecialColor=false,LabelAlignment=Center,MaxWidth=100}),");
            s.WriteLine("  East=(),");
            s.WriteLine("  SouthEast=(),");
            s.WriteLine("  North=(),");
            s.WriteLine("  West=(),");
            s.WriteLine("  SouthWest=(),");
            s.WriteLine("  NorthEast=(),");
            s.WriteLine("  South=(),");
            s.WriteLine("  NorthWest=();");
        }

        internal static void dump_edges(Funcdata data, TextWriter s)
        {
            s.WriteLine("\n\n// Add Edges");
            s.WriteLine("*CMD=*COLUMNAR_INPUT,");
            s.WriteLine("  Command=AddEdges,");
            s.WriteLine("  Parsing=WhiteSpace,");
            s.WriteLine("  Fields=({Name=*FromKey, Location=1},");
            s.WriteLine("          {Name=*ToKey, Location=2},");
            s.WriteLine("          {Name=Name, Location=3});\n");
            s.WriteLine("//START:edges");

            IEnumerator<PcodeOp> oiter = data.beginOpAlive();
            while (oiter.MoveNext()) {
                print_edges(oiter.Current, s);
            }
            s.WriteLine("*END_COLUMNS");
        }

        internal static void dump_varnode_vertex(Funcdata data, TextWriter s)
        {
            PcodeOp op;
            int i, start, stop;

            s.WriteLine("\n\n// Add Vertices");
            s.WriteLine("*CMD=*COLUMNAR_INPUT,");
            s.WriteLine("  Command=AddVertices,");
            s.WriteLine("  Parsing=WhiteSpace,");
            s.WriteLine("  Fields=({Name=Internal, Location=1},");
            s.WriteLine("          {Name=SubClass, Location=2},");
            s.WriteLine("          {Name=Type, Location=3},");
            s.WriteLine("          {Name=Name, Location=4},");
            s.WriteLine("          {Name=Address, Location=5});");
            s.WriteLine();
            s.WriteLine("//START:varnodes");

            IEnumerator<PcodeOp> oiter = data.beginOpAlive();
            while (oiter.MoveNext()) {
                op = oiter.Current;
                print_varnode_vertex(op.getOut(), s);
                start = 0;
                stop = op.numInput();
                switch (op.code()) {
                    case OpCode.CPUI_LOAD:
                    case OpCode.CPUI_STORE:
                    case OpCode.CPUI_BRANCH:
                    case OpCode.CPUI_CALL:
                        start = 1;
                        break;
                    case OpCode.CPUI_INDIRECT:
                        stop = 1;
                        break;
                    default:
                        break;
                }
                for (i = start; i < stop; ++i)
                    print_varnode_vertex(op.getIn(i), s);
            }
            s.WriteLine("*END_COLUMNS");
            oiter = data.beginOpAlive();
            while (oiter.MoveNext()) {
                op = oiter.Current;
                if (op.getOut() != (Varnode)null)
                    op.getOut().clearMark();
                for (i = 0; i < op.numInput(); ++i)
                    op.getIn(i).clearMark();
            }
        }

        internal static void dump_op_vertex(Funcdata data, TextWriter s)
        {
            PcodeOp op;

            s.WriteLine("\n\n// Add Vertices");
            s.WriteLine("*CMD=*COLUMNAR_INPUT,");
            s.WriteLine("  Command=AddVertices,");
            s.WriteLine("  Parsing=WhiteSpace,");
            s.WriteLine("  Fields=({Name=Internal, Location=1},");
            s.WriteLine("          {Name=SubClass, Location=2},");
            s.WriteLine("          {Name=Type, Location=3},");
            s.WriteLine("          {Name=Name, Location=4},");
            s.WriteLine("          {Name=Address, Location=5});");
            s.WriteLine();
            s.WriteLine("//START:opnodes");

            IEnumerator<PcodeOp> oiter = data.beginOpAlive();
            while (oiter.MoveNext()) {
                op = oiter.Current;
                print_op_vertex(op, s);
            }
            s.WriteLine("*END_COLUMNS");
        }

        internal static void dump_dom_edges(BlockGraph graph, TextWriter s, bool falsenode)
        {
            s.WriteLine("\n\n// Add Edges");
            s.WriteLine("*CMD=*COLUMNAR_INPUT,");
            s.WriteLine("  Command=AddEdges,");
            s.WriteLine("  Parsing=WhiteSpace,");
            s.WriteLine("  Fields=({Name=*FromKey, Location=1},");
            s.WriteLine("          {Name=*ToKey, Location=2});");
            s.WriteLine();

            for (int i = 0; i < graph.getSize(); ++i)
                print_dom_edge(graph.getBlock(i), s, falsenode);
            s.WriteLine("*END_COLUMNS");
        }

        private static void print_edges(PcodeOp op, TextWriter s)
        {
            Varnode? vn = op.getOut();
            if (vn != (Varnode)null)
                s.WriteLine($"o{op.getTime()} v{vn.getCreateIndex()} output");
            int start = 0;
            int stop = op.numInput();
            switch (op.code()) {
                case OpCode.CPUI_LOAD:
                case OpCode.CPUI_STORE:
                case OpCode.CPUI_BRANCH:
                case OpCode.CPUI_CALL:
                    start = 1;
                    break;
                case OpCode.CPUI_INDIRECT:
                    stop = 1;
                    break;
                default:
                    break;
            }
            for (int i = start; i < stop; ++i) {
                vn = op.getIn(i);
                spacetype tp = vn.getSpace().getType();
                if ((tp != spacetype.IPTR_FSPEC) && (tp != spacetype.IPTR_IOP))
                    s.WriteLine($"v{vn.getCreateIndex()} o{op.getTime()} input");
            }
        }
        
        private static void print_dom_edge(FlowBlock bl, TextWriter s, bool falsenode)
        {
            FlowBlock dom = bl.getImmedDom();

            if (dom != (FlowBlock)null)
                s.WriteLine($"{dom.getIndex()} {bl.getIndex()}");
            else if (falsenode)
                s.WriteLine("-1 {bl.getIndex()}");
        }
        
        private static void print_op_vertex(PcodeOp op, TextWriter s)
        {
            s.Write($"o{op.getTime()} ");
            if (op.isBranch())
                s.Write("branch");
            else if (op.isCall())
                s.Write("call");
            else if (op.isMarker())
                s.Write("marker");
            else
                s.Write("basic");
            s.Write(" op ");
            if (!op.getOpName().empty())
                s.Write(op.getOpName());
            else
                s.Write("unkop");
            s.Write($" {op.getAddr().getOffset():X}");
            s.WriteLine();
        }
        
        private static void print_varnode_vertex(Varnode vn, TextWriter s)
        {
            if (vn == (Varnode)null) return;
            if (vn.isMark()) return;
            AddrSpace spc = vn.getSpace();
            if (spc.getType() == spacetype.IPTR_FSPEC) return;
            if (spc.getType() == spacetype.IPTR_IOP) return;
            s.Write($"v{vn.getCreateIndex()} {spc.getName()}");
            s.Write(" var ");

            vn.printRawNoMarkup(s);

            PcodeOp? op = vn.getDef();
            if (op != (PcodeOp)null)
                s.Write($" {op.getAddr().getOffset():X}");
            else if (vn.isInput())
                s.Write(" i");
            else
                s.Write(" <na>");
            s.WriteLine();
            vn.setMark();
        }

        internal static void print_block_vertex(FlowBlock bl, TextWriter s)
        {
            s.Write($" {bl.sizeOut()}");
            s.Write($" {bl.sizeIn()}");
            s.Write($" {bl.getIndex()}");
            s.Write($" {bl.getStart().getOffset():X}");
            s.Write($" {bl.getStop().getOffset()}");
            s.WriteLine();
        }

        internal static void print_block_edge(FlowBlock bl, TextWriter s)
        {
            for (int i = 0; i < bl.sizeIn(); ++i)
                s.WriteLine($"{bl.getIn(i).getIndex()} {bl.getIndex()}");
        }
    }
}
