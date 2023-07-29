using Sla.EXTRA;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    internal class UnifyCPrinter
    {
        private List<UnifyDatatype> storemap = new List<UnifyDatatype>();
        private List<string> namemap = new List<string>();
        private int4 depth;
        private int4 printingtype;      // 0 = standard rule
        private string classname;       // Name of the printed class
        private int4 opparam;
        private List<OpCode> opcodelist;  // List of opcodes that are recognized by rule
        
        private void initializeBase(ConstraintGroup g)
        {
            grp = g;
            depth = 0;
            namemap.clear();
            storemap.clear();
            opparam = -1;
            opcodelist.clear();
            int4 maxop = g.getMaxNum();
            storemap.resize(maxop + 1, UnifyDatatype());

            g.collectTypes(storemap);

            for (int4 i = 0; i <= maxop; ++i)
            {
                ostringstream s;
                s << storemap[i].getBaseName() << dec << i;
                namemap.push_back(s.str());
            }
        }

        private void printGetOpList(TextWriter s)
        { // Print the getOpList method of the new rule
            s << "void " << classname << "::getOpList(List<uint4> &oplist) const" << endl;
            s << endl;
            s << '{' << endl;
            for (int4 i = 0; i < opcodelist.size(); ++i)
            {
                s << "  oplist.push_back(CPUI_" << get_opname(opcodelist[i]) << ");" << endl;
            }
            s << '}' << endl;
            s << endl;
        }

        private void printRuleHeader(TextWriter s)
        { // print the header for the applyOp method of the rule
            s << "int " << classname << "::applyOp(PcodeOp *" << namemap[opparam] << ",Funcdata &data)" << endl;
            s << endl;
            s << '{' << endl;
        }

        private ConstraintGroup grp;
        
        public UnifyCPrinter()
        {
            grp = (ConstraintGroup*)0;
            opparam = -1;
            printingtype = 0;
        }

        public int4 getDepth() => depth;

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
            for(int4 i=0;i<depth+1;++i) s << "  ";
        }

        public void printAbort(TextWriter s)
        {
            depth += 1;
            printIndent(s);
            if (depth > 1)
                s << "continue;";
            else
            {
                if (printingtype == 0)
                    s << "return 0;";
                else
                    s << "return false;";
            }
            depth -= 1;
            s << endl;
        }

        public void popDepth(TextWriter s, int4 newdepth)
        {
            while (depth != newdepth)
            {
                depth -= 1;
                printIndent(s);
                s << '}' << endl;
            }
        }

        public string getName(int4 id) => namemap[id];

        public void initializeRuleAction(ConstraintGroup g, int4 opparam, List<OpCode> olist)
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

        public void addNames(Dictionary<string, int4> nmmap)
        {
            map<string, int4>::const_iterator iter;

            for (iter = nmmap.begin(); iter != nmmap.end(); ++iter)
            {
                int4 slot = (*iter).second;
                if (namemap.size() <= slot)
                    throw new LowlevelError("Name indices do not match constraint");
                namemap[slot] = (*iter).first;
            }
        }

        public void printVarDecls(TextWriter s)
        { // Print the variables declarations
            for (int4 i = 0; i < namemap.size(); ++i)
            {
                if (i == opparam) continue;
                storemap[i].printVarDecl(s, i, *this);
            }
            if (namemap.size() != 0)
                s << endl;          // Extra blank line
        }

        public void print(TextWriter s)
        {
            if (printingtype == 0)
            {
                printGetOpList(s);
                s << endl;
                printRuleHeader(s);
                printVarDecls(s);
                grp.print(s, *this);
                printIndent(s);
                s << "return 1;" << endl;   // Found a complete match
                if (depth != 0)
                {
                    popDepth(s, 0);
                    printIndent(s);
                    s << "return 0;" << endl;   // Could never find a complete match
                }
                s << '}' << endl;
            }
            else if (printingtype == 1)
            {
                printVarDecls(s);
                grp.print(s, *this);
                printIndent(s);
                s << "return true;" << endl;
                if (depth != 0)
                {
                    popDepth(s, 0);
                    printIndent(s);
                    s << "return false;" << endl;
                }
                s << '}' << endl;
            }
        }
    }
}
