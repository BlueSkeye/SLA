using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
#if CPUI_RULECOMPILE
    internal class IfcParseRule : IfaceDecompCommand
    {
        public override void execute(TextReader s)
{ // Parse a rule and print it out as a C routine
  string filename;
  bool debug = false;

  s >> filename;
  if (filename.size() == 0)
    throw IfaceParseError("Missing rule input file");

  s >> ws;
  if (!s.eof()) {
    string val;
    s >> val;
    if ((val=="true")||(val=="debug"))
      debug = true;
  }
  ifstream thefile( filename.c_str());
  if (!thefile)
    throw IfaceExecutionError("Unable to open rule file: "+filename);

  RuleCompile ruler;
  ruler.setErrorStream(*status.optr);
  ruler.run(thefile,debug);
  if (ruler.numErrors() != 0) {
    *status.optr << "Parsing aborted on error" << endl;
    return;
  }
  int opparam;
  List<OpCode> opcodelist;
  opparam = ruler.postProcessRule(opcodelist);
  UnifyCPrinter cprinter;
  cprinter.initializeRuleAction(ruler.getRule(),opparam,opcodelist);
  cprinter.addNames(ruler.getNameMap());
  cprinter.print(*status.optr);
}
    }
#endif
}
