using Sla.CORE;
using Sla.DECCORE;

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
        // Program data for the test collection
        private IfaceDecompData dcp;
        // Name of the file containing test data
        private string fileName;
        // List of tests for this collection
        private List<FunctionTestProperty> testList = new List<FunctionTestProperty>();
        // Sequence of commands for current test
        private List<string> commands = new List<string>();
        // Decompiler console for executing scripts
        private IfaceStatus console;
        // Set to \b true if \b this object owns the console
        private bool consoleOwner;
        // Count of tests that were executed
        private /*mutable*/ int numTestsApplied;
        // Count of tests that passed
        private /*mutable*/ int numTestsSucceeded;

        /// Clear any previous architecture and function
        private void clear()
        {
            dcp.clearArchitecture();
            commands.Clear();
            testList.Clear();
            console.reset();
        }

        /// Reconstruct commands from an XML tag
        private void restoreXmlCommands(Element el)
        {
            foreach (Element subel in el.getChildren()) {
                commands.Add(subel.getContent());
            }
        }

        /// Build program (Architecture) from \<binaryimage> tag
        private void buildProgram(DocumentStorage store)
        {
            ArchitectureCapability? capa = ArchitectureCapability.getCapability("xml");
            if (capa == (ArchitectureCapability)null)
                throw new IfaceExecutionError("Missing XML architecture capability");
            dcp.conf = capa.buildArchitecture("test", "", console.optr);
            string errmsg = string.Empty;
            bool iserror = false;
            try {
                dcp.conf.init(docStorage);
                // Read in loader symbols
                dcp.conf.readLoaderSymbols("::");
            }
            catch (DecoderError err) {
                errmsg = err.ToString();
                iserror = true;
            } catch (CORE.LowlevelError err) {
                errmsg = err.ToString();
                iserror = true;
            }
            if (iserror)
                throw new IfaceExecutionError($"Error during architecture initialization: {errmsg}");
        }

        /// Initialize each FunctionTestProperty
        /// Let each test initialize itself thru its startTest() method
        private void startTests()
        {
            foreach (FunctionTestProperty property in testList) {
                property.startTest();
            }
        }

        /// Let all tests analyze a line of the results
        /// Each test gets a chance to process a line of output
        /// \param line is the given line of output
        private void passLineToTests(string line)
        {
            foreach (FunctionTestProperty property in testList) {
                property.processLine(line);
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
            foreach (FunctionTestProperty property in testList) {
                numTestsApplied += 1;
                if (property.endTest()) {
                    console.optr.WriteLine($"Success -- {property.getName()}");
                    numTestsSucceeded += 1;
                }
                else {
                    console.optr.WriteLine($"FAIL -- {property.getName()}");
                    lateStream.Add(property.getName());
                }
            }
        }

        /// \param s is the stream where output is sent during tests
        public FunctionTestCollection(TextWriter s)
        {
            console = new ConsoleCommands(s, commands);
            consoleOwner = true;
            dcp = (IfaceDecompData)console.getData("decompile");
            console.setErrorIsDone(true);
            numTestsApplied = 0;
            numTestsSucceeded = 0;
        }

        /// Constructor with preexisting console
        public FunctionTestCollection(IfaceStatus con)
        {
            console = con;
            consoleOwner = false;
            dcp = (IfaceDecompData)console.getData("decompile");
            numTestsApplied = 0;
            numTestsSucceeded = 0;
        }

        ~FunctionTestCollection()
        {
            //if (consoleOwner)
            //    delete console;
        }

        /// Get the number of tests executed
        public int getTestsApplied() => numTestsApplied;

        /// Get the number of tests that passed
        public int getTestsSucceeded() => numTestsSucceeded;

        /// Get the number of commands in the current script
        public int numCommands() => commands.size();

        /// Get the i-th command
        public string getCommand(int i) => commands[i];

        /// Load a test program, tests, and script
        /// Load the architecture based on the discovered \<binaryimage> tag.
        /// Collect the script commands and the specific tests.
        /// \param filename is the XML file holding the test data
        public void loadTest(string filename)
        {
            fileName = filename;
            DocumentStorage docStorage = new DocumentStorage();
            Document doc = docStorage.openDocument(filename);
            Element el = doc.getRoot() ?? throw new BugException();
            if (el.getName() == "decompilertest")
                restoreXml(docStorage, el);
            else if (el.getName() == "binaryimage")
                restoreXmlOldForm(docStorage, el);
            else
                throw new IfaceParseError($"Test file {filename} has unrecognized XML tag: {el.getName()}");
        }

        /// Load tests from a \<decompilertest> tag.
        public void restoreXml(DocumentStorage store, Element el)
        {
            bool sawScript = false;
            bool sawTests = false;
            bool sawProgram = false;
            foreach(Element subel in el.getChildren()) {
                if (subel.getName() == "script") {
                    sawScript = true;
                    restoreXmlCommands(subel);
                }
                else if (subel.getName() == "stringmatch") {
                    sawTests = true;
                    FunctionTestProperty newProperty = new FunctionTestProperty();
                    testList.Add(newProperty);
                    newProperty.restoreXml(subel);
                }
                else if (subel.getName() == "binaryimage") {
                    sawProgram = true;
                    store.registerTag(subel);
                    buildProgram(store);
                }
                else
                    throw new IfaceParseError("Unknown tag in <decompiletest>: " + subel.getName());
            }
            if (!sawScript)
                throw new IfaceParseError("Did not see <script> tag in <decompiletest>");
            if (!sawTests)
                throw new IfaceParseError("Did not see any <stringmatch> tags in <decompiletest>");
            if (!sawProgram)
                throw new IfaceParseError("No <binaryimage> tag in <decompiletest>");
        }

        /// Load tests from \<binaryimage> tag.
        /// Pull the script and tests from a comment in \<binaryimage>
        public void restoreXmlOldForm(DocumentStorage store, Element el)
        {
            throw new IfaceParseError("Old format test not supported");
        }

        /// Run the script and perform the tests
        /// Run the script commands on the current program.
        /// Collect any bulk output, and run tests over the output.
        /// Report test failures back to the caller
        /// \param lateStream collects messages for a final summary
        public void runTests(List<string> lateStream)
        {
            TextWriter origStream = console.optr;
            numTestsApplied = 0;
            numTestsSucceeded = 0;
            // Collect command console output
            TextWriter midBuffer = new StringWriter();
            console.optr = midBuffer;
            TextWriter bulkout = new StringWriter();
            console.fileoptr = bulkout;
            mainloop(console);
            console.optr = origStream;
            console.fileoptr = origStream;
            if (console.isInError()) {
                console.optr.WriteLine("Error: Did not apply tests in {fileName}");
                console.optr.WriteLine(midBuffer.ToString());
                lateStream.Add($"Execution failed for {fileName}");
                return;
            }
            string result = bulkout.ToString();
            if (result.Length == 0) {
                lateStream.Add($"No output for {fileName}");
                return;
            }
            startTests();
            int prevpos = 0;
            int pos = result.IndexOf('\n');
            while (-1 != pos) {
                string line = result.Substring(prevpos, pos - prevpos);
                passLineToTests(line);
                prevpos = pos + 1;
                pos = result.IndexOf('\n', prevpos);
            }
            if (prevpos != result.Length) {
                string line = result.Substring(prevpos);   // Process final line without a newline char
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
            int totalTestsApplied = 0;
            int totalTestsSucceeded = 0;
            List<string> failures = new List<string>();
            FunctionTestCollection testCollection = new FunctionTestCollection(s);
            for (int i = 0; i < testFiles.size(); ++i) {
                try {
                    testCollection.clear();
                    testCollection.loadTest(testFiles[i]);
                    testCollection.runTests(failures);
                    totalTestsApplied += testCollection.getTestsApplied();
                    totalTestsSucceeded += testCollection.getTestsSucceeded();
                }
                catch (IfaceParseError err) {
                    failures.Add($"Error parsing {testFiles[i]}: {err.ToString()}{fs.ToString()}\n");
                }
                catch (IfaceExecutionError err) {
                    failures.Add($"Error executing {testFiles[i]}: {err.ToString()}{fs.ToString()}\n");
                }
            }
            s.WriteLine();
            s.WriteLine($"Total tests applied = {totalTestsApplied}");
            s.WriteLine($"Total passing tests = { totalTestsSucceeded})");
            s.WriteLine();
            if (!failures.empty()) {
                s.WriteLine("Failures: ");
                foreach (string failure in failures) {
                    s.WriteLine($"  {failure}");
                }
            }
            return totalTestsApplied - totalTestsSucceeded;
        }
    }
}
