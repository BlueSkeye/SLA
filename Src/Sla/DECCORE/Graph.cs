using System;
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
        internal static void dump_varnode_vertex(Funcdata data, ostream s)
        {
            list<PcodeOp*>::const_iterator oiter;
            PcodeOp* op;
            int i, start, stop;

            s << "\n\n// Add Vertices\n";
            s << "*CMD=*COLUMNAR_INPUT,\n";
            s << "  Command=AddVertices,\n";
            s << "  Parsing=WhiteSpace,\n";
            s << "  Fields=({Name=Internal, Location=1},\n";
            s << "          {Name=SubClass, Location=2},\n";
            s << "          {Name=Type, Location=3},\n";
            s << "          {Name=Name, Location=4},\n";
            s << "          {Name=Address, Location=5});\n\n";
            s << "//START:varnodes\n";

            for (oiter = data.beginOpAlive(); oiter != data.endOpAlive(); ++oiter)
            {
                op = *oiter;
                print_varnode_vertex(op.getOut(), s);
                start = 0;
                stop = op.numInput();
                switch (op.code())
                {
                    case CPUI_LOAD:
                    case CPUI_STORE:
                    case CPUI_BRANCH:
                    case CPUI_CALL:
                        start = 1;
                        break;
                    case CPUI_INDIRECT:
                        stop = 1;
                        break;
                    default:
                        break;
                }
                for (i = start; i < stop; ++i)
                    print_varnode_vertex(op.getIn(i), s);
            }
            s << "*END_COLUMNS\n";
            for (oiter = data.beginOpAlive(); oiter != data.endOpAlive(); ++oiter)
            {
                op = *oiter;
                if (op.getOut() != (Varnode*)0)
                    op.getOut().clearMark();
                for (i = 0; i < op.numInput(); ++i)
                    op.getIn(i).clearMark();
            }
        }

        internal static void dump_op_vertex(Funcdata data, ostream s)
        {
            list<PcodeOp*>::const_iterator oiter;
            PcodeOp* op;

            s << "\n\n// Add Vertices\n";
            s << "*CMD=*COLUMNAR_INPUT,\n";
            s << "  Command=AddVertices,\n";
            s << "  Parsing=WhiteSpace,\n";
            s << "  Fields=({Name=Internal, Location=1},\n";
            s << "          {Name=SubClass, Location=2},\n";
            s << "          {Name=Type, Location=3},\n";
            s << "          {Name=Name, Location=4},\n";
            s << "          {Name=Address, Location=5});\n\n";
            s << "//START:opnodes\n";

            for (oiter = data.beginOpAlive(); oiter != data.endOpAlive(); ++oiter)
            {
                op = *oiter;
                print_op_vertex(op, s);
            }
            s << "*END_COLUMNS\n";
        }

        internal static void dump_dom_edges(BlockGraph graph, ostream s, bool falsenode)
        {
            s << "\n\n// Add Edges\n";
            s << "*CMD=*COLUMNAR_INPUT,\n";
            s << "  Command=AddEdges,\n";
            s << "  Parsing=WhiteSpace,\n";
            s << "  Fields=({Name=*FromKey, Location=1},\n";
            s << "          {Name=*ToKey, Location=2});\n\n";

            for (int i = 0; i < graph.getSize(); ++i)
                print_dom_edge(graph.getBlock(i), s, falsenode);
            s << "*END_COLUMNS\n";
        }
    }
}
