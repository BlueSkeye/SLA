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
                    string::size_type pos = preproc.find('=');
                    if (pos == string::npos)
                    {
                        cerr << "Bad sleigh option: " << argv[i] << endl;
                        exit(1);
                    }
                    string name = preproc.substr(0, pos);
                    string value = preproc.substr(pos + 1);
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
  
            if (compileAll)
            {

                if (i < argc - 1)
                {
                    cerr << "Too many parameters" << endl;
                    exit(1);
                }
                string::size_type slaspecExtLen = SLASPECEXT.length();

                List<string> slaspecs;
                string dirStr = ".";
                if (i != argc)
                    dirStr = argv[i];
                findSlaSpecs(slaspecs, dirStr, SLASPECEXT);
                cout << "Compiling " << dec << slaspecs.size() << " slaspec files in " << dirStr << endl;
                for (int j = 0; j < slaspecs.size(); ++j)
                {
                    string slaspec = slaspecs[j];
                    cout << "Compiling (" << dec << (j + 1) << " of " << dec << slaspecs.size() << ") " << slaspec << endl;
                    string sla = slaspec;
                    sla.replace(slaspec.length() - slaspecExtLen, slaspecExtLen, SLAEXT);
                    SleighCompile compiler;
                    compiler.setAllOptions(defines, unnecessaryPcodeWarning, lenientConflict, allCollisionWarning, allNopWarning,
                               deadTempWarning, enforceLocalKeyWord, largeTemporaryWarning, caseSensitiveRegisterNames);
                    retval = compiler.run_compilation(slaspec, sla);
                    if (retval != 0)
                    {
                        return retval; // stop on first error
                    }
                }

            }
            else
            { // compile single specification

                if (i == argc)
                {
                    cerr << "Missing input file name" << endl;
                    exit(1);
                }

                string fileinExamine(argv[i]);

                string::size_type extInPos = fileinExamine.find(SLASPECEXT);
                bool autoExtInSet = false;
                bool extIsSLASPECEXT = false;
                string fileinPreExt = "";
                if (extInPos == string::npos)
                { //No Extension Given...
                    fileinPreExt = fileinExamine;
                    fileinExamine.append(SLASPECEXT);
                    autoExtInSet = true;
                }
                else
                {
                    fileinPreExt = fileinExamine.substr(0, extInPos);
                    extIsSLASPECEXT = true;
                }

                if (i < argc - 2)
                {
                    cerr << "Too many parameters" << endl;
                    exit(1);
                }

                SleighCompile compiler;
                compiler.setAllOptions(defines, unnecessaryPcodeWarning, lenientConflict, allCollisionWarning, allNopWarning,
                           deadTempWarning, enforceLocalKeyWord, largeTemporaryWarning, caseSensitiveRegisterNames);

                if (i < argc - 1)
                {
                    string fileoutExamine(argv[i + 1]);
                    string::size_type extOutPos = fileoutExamine.find(SLAEXT);
                    if (extOutPos == string::npos)
                    { // No Extension Given...
                        fileoutExamine.append(SLAEXT);
                    }
                    retval = compiler.run_compilation(fileinExamine, fileoutExamine);
                }
                else
                {
                    // First determine whether or not to use Run_XML...
                    if (autoExtInSet || extIsSLASPECEXT)
                    {   // Assumed format of at least "sleigh file" . "sleigh file.slaspec file.sla"
                        string fileoutSTR = fileinPreExt;
                        fileoutSTR.append(SLAEXT);
                        retval = compiler.run_compilation(fileinExamine, fileoutSTR);
                    }
                    else
                    {
                        retval = run_xml(fileinExamine, compiler);
                    }

                }
            }
            return retval;
        }
    }
}
