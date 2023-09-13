using Sla.CORE;
using Sla.EXTRA;

namespace Sla.EXTRA
{
    internal class UnifyCPrinter
    {
        private List<UnifyDatatype> storemap = new List<UnifyDatatype>();
        private List<string> namemap = new List<string>();
        private int depth;
        // 0 = standard rule
        private int printingtype;
        // Name of the printed class
        private string classname;
        private int opparam;
        // List of opcodes that are recognized by rule
        private List<OpCode> opcodelist = new List<OpCode>();
        
        private void initializeBase(ConstraintGroup g)
        {
            grp = g;
            depth = 0;
            namemap.Clear();
            storemap.Clear();
            opparam = -1;
            opcodelist.Clear();
            int maxop = g.getMaxNum();
            storemap.resize(maxop + 1, new UnifyDatatype());

            g.collectTypes(storemap);

            for (int i = 0; i <= maxop; ++i) {
                TextWriter s = new StringWriter();
                s.Write($"{storemap[i].getBaseName()}{i}");
                namemap.Add(s.ToString());
            }
        }

        private void printGetOpList(TextWriter s)
        {
            // Print the getOpList method of the new rule
            s.WriteLine($"void {classname}::getOpList(List<OpCode> &oplist) const");
            s.WriteLine();
            s.WriteLine('{');
            for (int i = 0; i < opcodelist.size(); ++i) {
                s.WriteLine($"  oplist.Add(OpCode.CPUI_{Globals.get_opname(opcodelist[i])});");
            }
            s.WriteLine('}');
            s.WriteLine();
        }

        private void printRuleHeader(TextWriter s)
        {
            // print the header for the applyOp method of the rule
            s.WriteLine($"int {classname}::applyOp(PcodeOp *{namemap[opparam]},Funcdata &data)");
            s.WriteLine();
            s.WriteLine('{');
        }

        private ConstraintGroup? grp;
        
        public UnifyCPrinter()
        {
            grp = (ConstraintGroup)null;
            opparam = -1;
            printingtype = 0;
        }

        public int getDepth() => depth;

        public void incDepth()
        {
            depth += 1;
        }

        public void decDepth()
        {
            depth -= 1;
        }

        public void printIndent(TextWriter s) 
        {
            for(int i=0;i<depth+1;++i) s.Write("  ");
        }

        public void printAbort(TextWriter s)
        {
            depth += 1;
            printIndent(s);
            if (depth > 1)
                s.Write("continue;");
            else {
                if (printingtype == 0)
                    s.Write("return 0;");
                else
                    s.Write("return false;");
            }
            depth -= 1;
            s.WriteLine();
        }

        public void popDepth(TextWriter s, int newdepth)
        {
            while (depth != newdepth) {
                depth -= 1;
                printIndent(s);
                s.WriteLine('}');
            }
        }

        public string getName(int id) => namemap[id];

        public void initializeRuleAction(ConstraintGroup g, int opparam, List<OpCode> olist)
        {
            initializeBase(g);
            printingtype = 0;
            classname = "DummyRule";

            opparam = opp;
            opcodelist = oplist;
        }

        public void initializeBasic(ConstraintGroup g)
        {
            initializeBase(g);
            printingtype = 1;
            opparam = -1;
        }

        public void setClassName(string nm)
        {
            classname = nm;
        }

        public void addNames(Dictionary<string, int> nmmap)
        {
            IEnumerator<KeyValuePair<string, int>> iter = nmmap.GetEnumerator();

            while (iter.MoveNext()) {
                int slot = iter.Current.Value;
                if (namemap.size() <= slot) {
                    throw new LowlevelError("Name indices do not match constraint");
                }
                namemap[slot] = iter.Current.Key;
            }
        }

        public void printVarDecls(TextWriter s)
        {
            // Print the variables declarations
            for (int i = 0; i < namemap.size(); ++i) {
                if (i == opparam) continue;
                storemap[i].printVarDecl(s, i, this);
            }
            if (namemap.size() != 0)
                // Extra blank line
                s.WriteLine();
        }

        public void print(TextWriter s)
        {
            if (printingtype == 0) {
                printGetOpList(s);
                s.WriteLine();
                printRuleHeader(s);
                printVarDecls(s);
                grp.print(s, this);
                printIndent(s);
                // Found a complete match
                s.WriteLine("return 1;");
                if (depth != 0) {
                    popDepth(s, 0);
                    printIndent(s);
                    // Could never find a complete match
                    s.WriteLine("return 0;");
                }
                s.WriteLine('}');
            }
            else if (printingtype == 1) {
                printVarDecls(s);
                grp.print(s, this);
                printIndent(s);
                s.WriteLine("return true;");
                if (depth != 0) {
                    popDepth(s, 0);
                    printIndent(s);
                    s.WriteLine("return false;");
                }
                s.WriteLine('}');
            }
        }
    }
}
