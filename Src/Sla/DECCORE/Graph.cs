using Sla.CORE;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Sla.DECCORE
{
    internal static class Graph
    {
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
            s.WriteLine("          {Name=*ToKey, Location=2});";
            s.WriteLine();

            for (int i = 0; i < graph.getSize(); ++i)
                print_dom_edge(graph.getBlock(i), s, falsenode);
            s.WriteLine("*END_COLUMNS");
        }
    }
}
