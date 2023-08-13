using Sla.DECCORE;
using Sla.SLACOMP;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.ConstrainedExecution;
using System.Text;
using System.Threading.Tasks;

namespace Sla.SLACOMP
{
    public static class Sleigh
    {
        public static int Main(string[] argv)
        {
            // using namespace ghidra;

            int retval = 0;

            signal(SIGSEGV, &segvHandler); // Exit on SEGV errors

#if YYDEBUG
            sleighdebug = 0;
#endif

            if (argv.Length < 2) {
                Console.Error.WriteLine("USAGE: sleigh [-x] [-dNAME=VALUE] inputfile [outputfile]");
                Console.Error.WriteLine("   -a              scan for all slaspec files recursively where inputfile is a directory");
                Console.Error.WriteLine("   -x              turns on parser debugging");
                Console.Error.WriteLine("   -u              print warnings for unnecessary pcode instructions");
                Console.Error.WriteLine("   -l              report pattern conflicts");
                Console.Error.WriteLine("   -n              print warnings for all NOP constructors");
                Console.Error.WriteLine("   -t              print warnings for dead temporaries");
                Console.Error.WriteLine("   -e              enforce use of 'local' keyword for temporaries");
                Console.Error.WriteLine("   -c              print warnings for all constructors with colliding operands");
                Console.Error.WriteLine("   -o              print warnings for temporaries which are too large");
                Console.Error.WriteLine("   -s              treat register names as case sensitive");
                Console.Error.WriteLine("   -DNAME=VALUE    defines a preprocessor macro NAME with value VALUE");
                return 2;
            }

            const string SLAEXT = ".sla";    // Default sla extension
            const string SLASPECEXT = ".slaspec";
            Dictionary<string, string> defines = new Dictionary<string, string>();
            bool unnecessaryPcodeWarning = false;
            bool lenientConflict = true;
            bool allCollisionWarning = false;
            bool allNopWarning = false;
            bool deadTempWarning = false;
            bool enforceLocalKeyWord = false;
            bool largeTemporaryWarning = false;
            bool caseSensitiveRegisterNames = false;
            bool compileAll = false;
            SleighCompile compiler;

            int i;
            int argCount = argv.Length;
            for (i = 1; i < argCount; ++i) {
                if (argv[i][0] != '-') break;
                if (argv[i][1] == 'a')
                    compileAll = true;
                else if (argv[i][1] == 'D') {
                    string preproc = argv[i].Substring(2);
                    int pos = preproc.IndexOf('=');
                    if (0 > pos) {
                        Console.Error.WriteLine($"Bad sleigh option: {argv[i]}");
                        return 1;
                    }
                    string name = preproc.Substring(0, pos);
                    string value = preproc.Substring(pos + 1);
                    defines[name] = value;
                }
                else if (argv[i][1] == 'u')
                    unnecessaryPcodeWarning = true;
                else if (argv[i][1] == 'l')
                    lenientConflict = false;
                else if (argv[i][1] == 'c')
                    allCollisionWarning = true;
                else if (argv[i][1] == 'n')
                    allNopWarning = true;
                else if (argv[i][1] == 't')
                    deadTempWarning = true;
                else if (argv[i][1] == 'e')
                    enforceLocalKeyWord = true;
                else if (argv[i][1] == 'o')
                    largeTemporaryWarning = true;
                else if (argv[i][1] == 's')
                    caseSensitiveRegisterNames = true;
#if YYDEBUG
                else if (argv[i][1] == 'x')
                    sleighdebug = 1;        // Debug option
#endif
                else {
                    Console.Error.WriteLine($"Unknown option: {argv[i]}");
                    return 1;
                }
            }
  
            if (compileAll) {
                if (i < argCount - 1) {
                    Console.Error.WriteLine("Too many parameters");
                    return 1;
                }
                int slaspecExtLen = SLASPECEXT.Length;

                List<string> slaspecs = new List<string>();
                string dirStr = ".";
                if (i != argCount)
                    dirStr = argv[i];
                Globals.findSlaSpecs(slaspecs, dirStr, SLASPECEXT);
                Console.Out.WriteLine($"Compiling {slaspecs.size()} slaspec files in {dirStr}");
                for (int j = 0; j < slaspecs.size(); ++j) {
                    string slaspec = slaspecs[j];
                    Console.Out.WriteLine("Compiling ({(j + 1)} of {slaspecs.size()}) {slaspec}");
                    string sla = slaspec;
                    sla = sla.Replace(slaspec.Length - slaspecExtLen, slaspecExtLen, SLAEXT);
                    compiler = new SleighCompile();
                    compiler.setAllOptions(defines, unnecessaryPcodeWarning, lenientConflict, allCollisionWarning, allNopWarning,
                        deadTempWarning, enforceLocalKeyWord, largeTemporaryWarning, caseSensitiveRegisterNames);
                    retval = compiler.run_compilation(slaspec, sla);
                    if (retval != 0) {
                        // stop on first error
                        break;
                    }
                }
                return retval;
            }
            // compile single specification
            if (i == argCount) {
                Console.Error.WriteLine("Missing input file name");
                return 1;
            }

            string fileinExamine = argv[i];

            int extInPos = fileinExamine.IndexOf(SLASPECEXT);
            bool autoExtInSet = false;
            bool extIsSLASPECEXT = false;
            string fileinPreExt = "";
            if (-1 == extInPos) {
                //No Extension Given...
                fileinPreExt = fileinExamine;
                fileinExamine += SLASPECEXT;
                autoExtInSet = true;
            }
            else {
                fileinPreExt = fileinExamine.Substring(0, extInPos);
                extIsSLASPECEXT = true;
            }

            if (i < argCount - 2) {
                Console.Error.WriteLine("Too many parameters");
                return 1;
            }

            compiler = new SleighCompile();
            compiler.setAllOptions(defines, unnecessaryPcodeWarning, lenientConflict, allCollisionWarning,
                allNopWarning, deadTempWarning, enforceLocalKeyWord, largeTemporaryWarning,
                caseSensitiveRegisterNames);
            if (i < argCount - 1) {
                string fileoutExamine = argv[i + 1];
                int extOutPos = fileoutExamine.IndexOf(SLAEXT);
                if (-1 == extOutPos) {
                    // No Extension Given...
                    fileoutExamine += SLAEXT;
                }
                return compiler.run_compilation(fileinExamine, fileoutExamine);
            }
            // First determine whether or not to use Run_XML...
            if (autoExtInSet || extIsSLASPECEXT) {
                // Assumed format of at least "sleigh file" . "sleigh file.slaspec file.sla"
                string fileoutSTR = fileinPreExt;
                fileoutSTR += SLAEXT;
                return compiler.run_compilation(fileinExamine, fileoutSTR);
            }
            return Globals.run_xml(fileinExamine, compiler);
        }
    }
}
