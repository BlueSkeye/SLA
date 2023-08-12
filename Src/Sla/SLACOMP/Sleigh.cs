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

            if (argc < 2) {
                cerr << "USAGE: sleigh [-x] [-dNAME=VALUE] inputfile [outputfile]" << endl;
                cerr << "   -a              scan for all slaspec files recursively where inputfile is a directory" << endl;
                cerr << "   -x              turns on parser debugging" << endl;
                cerr << "   -u              print warnings for unnecessary pcode instructions" << endl;
                cerr << "   -l              report pattern conflicts" << endl;
                cerr << "   -n              print warnings for all NOP constructors" << endl;
                cerr << "   -t              print warnings for dead temporaries" << endl;
                cerr << "   -e              enforce use of 'local' keyword for temporaries" << endl;
                cerr << "   -c              print warnings for all constructors with colliding operands" << endl;
                cerr << "   -o              print warnings for temporaries which are too large" << endl;
                cerr << "   -s              treat register names as case sensitive" << endl;
                cerr << "   -DNAME=VALUE    defines a preprocessor macro NAME with value VALUE" << endl;
                exit(2);
            }

            const string SLAEXT = ".sla";    // Default sla extension
            const string SLASPECEXT = ".slaspec";
            Dictionary<string, string> defines;
            bool unnecessaryPcodeWarning = false;
            bool lenientConflict = true;
            bool allCollisionWarning = false;
            bool allNopWarning = false;
            bool deadTempWarning = false;
            bool enforceLocalKeyWord = false;
            bool largeTemporaryWarning = false;
            bool caseSensitiveRegisterNames = false;

            bool compileAll = false;

            int i;
            for (i = 1; i < argc; ++i)
            {
                if (argv[i][0] != '-') break;
                if (argv[i][1] == 'a')
                    compileAll = true;
                else if (argv[i][1] == 'D')
                {
                    string preproc = argv[i].Substring(2);
                    int pos = preproc.IndexOf('=');
                    if (0 > pos) {
                        cerr.WriteLine($"Bad sleigh option: {argv[i]}");
                        exit(1);
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
                else
                {
                    cerr << "Unknown option: " << argv[i] << endl;
                    exit(1);
                }
            }
  
            if (compileAll) {
                if (i < argc - 1) {
                    cerr.WriteLine("Too many parameters");
                    exit(1);
                }
                int slaspecExtLen = SLASPECEXT.Length;

                List<string> slaspecs;
                string dirStr = ".";
                if (i != argc)
                    dirStr = argv[i];
                Globals.findSlaSpecs(slaspecs, dirStr, SLASPECEXT);
                cout.WriteLine($"Compiling {slaspecs.size()} slaspec files in {dirStr}");
                for (int j = 0; j < slaspecs.size(); ++j) {
                    string slaspec = slaspecs[j];
                    cout.WriteLine("Compiling ({(j + 1)} of {slaspecs.size()}) {slaspec}");
                    string sla = slaspec;
                    sla = sla.Replace(slaspec.Length - slaspecExtLen, slaspecExtLen, SLAEXT);
                    SleighCompile compiler = new SleighCompile();
                    compiler.setAllOptions(defines, unnecessaryPcodeWarning, lenientConflict, allCollisionWarning, allNopWarning,
                        deadTempWarning, enforceLocalKeyWord, largeTemporaryWarning, caseSensitiveRegisterNames);
                    retval = compiler.run_compilation(slaspec, sla);
                    if (retval != 0) {
                        return retval; // stop on first error
                    }
                }

            }
            else {
                // compile single specification
                if (i == argc) {
                    cerr.WriteLie("Missing input file name");
                    exit(1);
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

                if (i < argc - 2) {
                    cerr.WriteLine("Too many parameters");
                    exit(1);
                }

                SleighCompile compiler = new SleighCompile();
                compiler.setAllOptions(defines, unnecessaryPcodeWarning, lenientConflict, allCollisionWarning,
                    allNopWarning, deadTempWarning, enforceLocalKeyWord, largeTemporaryWarning,
                    caseSensitiveRegisterNames);
                if (i < argc - 1) {
                    string fileoutExamine = argv[i + 1];
                    int extOutPos = fileoutExamine.IndexOf(SLAEXT);
                    if (-1 == extOutPos) {
                        // No Extension Given...
                        fileoutExamine += SLAEXT;
                    }
                    retval = compiler.run_compilation(fileinExamine, fileoutExamine);
                }
                else {
                    // First determine whether or not to use Run_XML...
                    if (autoExtInSet || extIsSLASPECEXT) {
                        // Assumed format of at least "sleigh file" . "sleigh file.slaspec file.sla"
                        string fileoutSTR = fileinPreExt;
                        fileoutSTR += SLAEXT;
                        retval = compiler.run_compilation(fileinExamine, fileoutSTR);
                    }
                    else {
                        retval = Globals.run_xml(fileinExamine, compiler);
                    }

                }
            }
            return retval;
        }
    }
}
