using Sla.CORE;
using Sla.DECCORE;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    /// \brief A collection of tests around a single program/function
    ///
    /// The collection of tests is loaded from a single XML file via loadTest(),
    /// and the tests are run by calling runTests().
    /// An entire program is loaded and possibly annotated by a series of
    /// console command lines.  Decompiler output is also triggered by a command,
    /// and then the output is scanned for by the test objects (FunctionTestProperty).
    /// Results of passed/failed tests are collected.  If the command line script
    /// does not complete properly, this is considered a special kind of failure.
    internal class FunctionTestCollection
    {
        private IfaceDecompData dcp;       ///< Program data for the test collection
        private string fileName;        ///< Name of the file containing test data
        private List<FunctionTestProperty> testList = new List<FunctionTestProperty>();    ///< List of tests for this collection
        private List<string> commands = new List<string>();    ///< Sequence of commands for current test
        private IfaceStatus console;       ///< Decompiler console for executing scripts
        private bool consoleOwner;      ///< Set to \b true if \b this object owns the console
        private /*mutable*/ int4 numTestsApplied;       ///< Count of tests that were executed
        private /*mutable*/ int4 numTestsSucceeded; ///< Count of tests that passed

        /// Clear any previous architecture and function
        private void clear()
        {
            dcp.clearArchitecture();
            commands.clear();
            testList.clear();
            console.reset();
        }

        /// Reconstruct commands from an XML tag
        private void restoreXmlCommands(Element el)
        {
            List list = el.getChildren();
            List::const_iterator iter;

            for (iter = list.begin(); iter != list.end(); ++iter)
            {
                Element subel = *iter;
                commands.push_back(subel.getContent());
            }
        }

        /// Build program (Architecture) from \<binaryimage> tag
        private void buildProgram(DocumentStorage store)
        {
            ArchitectureCapability* capa = ArchitectureCapability::getCapability("xml");
            if (capa == (ArchitectureCapability*)0)
                throw IfaceExecutionError("Missing XML architecture capability");
            dcp.conf = capa.buildArchitecture("test", "", console.optr);
            string errmsg;
            bool iserror = false;
            try
            {
                dcp.conf.init(docStorage);
                dcp.conf.readLoaderSymbols("::"); // Read in loader symbols
            }
            catch (DecoderError err) {
                errmsg = err.ToString();
                iserror = true;
            } catch (LowlevelError err) {
                errmsg = err.ToString();
                iserror = true;
            }
            if (iserror)
                throw IfaceExecutionError("Error during architecture initialization: " + errmsg);
        }

        /// Initialize each FunctionTestProperty
        /// Let each test initialize itself thru its startTest() method
        private void startTests()
        {
            list<FunctionTestProperty>::const_iterator iter;
            for (iter = testList.begin(); iter != testList.end(); ++iter)
            {
                (*iter).startTest();
            }
        }

        /// Let all tests analyze a line of the results
        /// Each test gets a chance to process a line of output
        /// \param line is the given line of output
        private void passLineToTests(string line)
        {
            list<FunctionTestProperty>::const_iterator iter;
            for (iter = testList.begin(); iter != testList.end(); ++iter)
            {
                (*iter).processLine(line);
            }
        }

        /// \brief Do the final evaluation of each test
        ///
        /// This is called after each test has been fed all lines of output.
        /// The result of each test is printed to the \e midStream, and then
        /// failures are written to the lateStream in order to see a summary.
        /// \param lateStream collects failures to display as a summary
        private void evaluateTests(List<string> lateStream)
        {
            list<FunctionTestProperty>::const_iterator iter;
            for (iter = testList.begin(); iter != testList.end(); ++iter)
            {
                numTestsApplied += 1;
                if ((*iter).endTest())
                {
                    *console.optr << "Success -- " << (*iter).getName() << endl;
                    numTestsSucceeded += 1;
                }
                else
                {
                    *console.optr << "FAIL -- " << (*iter).getName() << endl;
                    lateStream.push_back((*iter).getName());
                }
            }
        }

        /// \param s is the stream where output is sent during tests
        public FunctionTestCollection(TextWriter s)
        {
            console = new ConsoleCommands(s, commands);
            consoleOwner = true;
            dcp = (IfaceDecompData*)console.getData("decompile");
            console.setErrorIsDone(true);
            numTestsApplied = 0;
            numTestsSucceeded = 0;
        }

        /// Constructor with preexisting console
        public FunctionTestCollection(IfaceStatus con)
        {
            console = con;
            consoleOwner = false;
            dcp = (IfaceDecompData*)console.getData("decompile");
            numTestsApplied = 0;
            numTestsSucceeded = 0;
        }

        ~FunctionTestCollection()
        {
            if (consoleOwner)
                delete console;
        }

        /// Get the number of tests executed
        public int4 getTestsApplied() => numTestsApplied;

        /// Get the number of tests that passed
        public int4 getTestsSucceeded() => numTestsSucceeded;

        /// Get the number of commands in the current script
        public int4 numCommands() => commands.size();

        /// Get the i-th command
        public string getCommand(int4 i) => commands[i];

        /// Load a test program, tests, and script
        /// Load the architecture based on the discovered \<binaryimage> tag.
        /// Collect the script commands and the specific tests.
        /// \param filename is the XML file holding the test data
        public void loadTest(string filename)
        {
            fileName = filename;
            DocumentStorage docStorage;
            Document* doc = docStorage.openDocument(filename);
            Element* el = doc.getRoot();
            if (el.getName() == "decompilertest")
                restoreXml(docStorage, el);
            else if (el.getName() == "binaryimage")
                restoreXmlOldForm(docStorage, el);
            else
                throw IfaceParseError("Test file " + filename + " has unrecognized XML tag: " + el.getName());
        }

        /// Load tests from a \<decompilertest> tag.
        public void restoreXml(DocumentStorage store, Element el)
        {
            List list = el.getChildren();
            List::const_iterator iter = list.begin();
            bool sawScript = false;
            bool sawTests = false;
            bool sawProgram = false;
            while (iter != list.end())
            {
                Element subel = *iter;
                ++iter;
                if (subel.getName() == "script")
                {
                    sawScript = true;
                    restoreXmlCommands(subel);
                }
                else if (subel.getName() == "stringmatch")
                {
                    sawTests = true;
                    testList.emplace_back();
                    testList.back().restoreXml(subel);
                }
                else if (subel.getName() == "binaryimage")
                {
                    sawProgram = true;
                    store.registerTag(subel);
                    buildProgram(store);
                }
                else
                    throw IfaceParseError("Unknown tag in <decompiletest>: " + subel.getName());
            }
            if (!sawScript)
                throw IfaceParseError("Did not see <script> tag in <decompiletest>");
            if (!sawTests)
                throw IfaceParseError("Did not see any <stringmatch> tags in <decompiletest>");
            if (!sawProgram)
                throw IfaceParseError("No <binaryimage> tag in <decompiletest>");
        }

        /// Load tests from \<binaryimage> tag.
        /// Pull the script and tests from a comment in \<binaryimage>
        public void restoreXmlOldForm(DocumentStorage store, Element el)
        {
            throw IfaceParseError("Old format test not supported");
        }

        /// Run the script and perform the tests
        /// Run the script commands on the current program.
        /// Collect any bulk output, and run tests over the output.
        /// Report test failures back to the caller
        /// \param lateStream collects messages for a final summary
        public void runTests(List<string> lateStream)
        {
            ostream* origStream = console.optr;
            numTestsApplied = 0;
            numTestsSucceeded = 0;
            ostringstream midBuffer;        // Collect command console output
            console.optr = &midBuffer;
            ostringstream bulkout;
            console.fileoptr = &bulkout;
            mainloop(console);
            console.optr = origStream;
            console.fileoptr = origStream;
            if (console.isInError())
            {
                *console.optr << "Error: Did not apply tests in " << fileName << endl;
                *console.optr << midBuffer.str() << endl;
                ostringstream fs;
                fs << "Execution failed for " << fileName;
                lateStream.push_back(fs.str());
                return;
            }
            string result = bulkout.str();
            if (result.size() == 0)
            {
                ostringstream fs;
                fs << "No output for " << fileName;
                lateStream.push_back(fs.str());
                return;
            }
            startTests();
            string::size_type prevpos = 0;
            string::size_type pos = result.find_first_of('\n');
            while (pos != string::npos)
            {
                string line = result.substr(prevpos, pos - prevpos);
                passLineToTests(line);
                prevpos = pos + 1;
                pos = result.find_first_of('\n', prevpos);
            }
            if (prevpos != result.size())
            {
                string line = result.substr(prevpos);   // Process final line without a newline char
                passLineToTests(line);
            }
            evaluateTests(lateStream);
        }

        /// Run tests for each listed file
        /// Run through all XML files in the given list, processing each in turn.
        /// \param testFiles is the given list of test files
        /// \param s is the output stream to print results to
        public static int runTestFiles(List<string> testFiles, TextWriter s)
        {
            int4 totalTestsApplied = 0;
            int4 totalTestsSucceeded = 0;
            list<string> failures;
            FunctionTestCollection testCollection(s);
            for (int4 i = 0; i < testFiles.size(); ++i)
            {
                try
                {
                    testCollection.clear();
                    testCollection.loadTest(testFiles[i]);
                    testCollection.runTests(failures);
                    totalTestsApplied += testCollection.getTestsApplied();
                    totalTestsSucceeded += testCollection.getTestsSucceeded();
                }
                catch (IfaceParseError err) {
                    ostringstream fs;
                    fs << "Error parsing " << testFiles[i] << ": " << err.ToString();
                    s << fs.str() << endl;
                    failures.push_back(fs.str());
                } catch (IfaceExecutionError err) {
                    ostringstream fs;
                    fs << "Error executing " << testFiles[i] << ": " << err.ToString();
                    s << fs.str() << endl;
                    failures.push_back(fs.str());
                }
            }

            s << endl;
            s << "Total tests applied = " << totalTestsApplied << endl;
            s << "Total passing tests = " << totalTestsSucceeded << endl;
            s << endl;
            if (!failures.empty()) {
                s << "Failures: " << endl;
                list<string>::const_iterator iter = failures.begin();
                for (int4 i = 0; i < 10; ++i) {
                    s << "  " << *iter << endl;
                    ++iter;
                    if (iter == failures.end()) break;
                }
            }
            return totalTestsApplied - totalTestsSucceeded;
        }
    }
}
