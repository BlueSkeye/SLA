using Sla.CORE;
using System.Text.RegularExpressions;

namespace Sla.EXTRA
{
    /// \brief A single property to be searched for in the output of a function decompilation
    ///
    /// This is generally a regular expression run over the characters in the
    /// decompiled "source" form of the function.
    /// The property may "match" more than once or not at all.
    internal class FunctionTestProperty
    {
        // Minimum number of times property is expected to match
        private int minimumMatch;
        // Maximum number of times property is expected to match
        private int maximumMatch;
        // Name of the test, to be printed in test summaries
        private string name;
        // Regular expression to match against a line of output
        private Regex pattern;
        // Number of times regular expression has been seen
        private /*mutable*/ uint count;

        /// Get the name of the property
        public string getName() => name;

        /// Reset "state", counting number of matching lines
        public void startTest()
        {
            count = 0;
        }

        /// Search thru \e line, update state if match found
        public void processLine(string line)
        {
            if (pattern == null) throw new ApplicationException();
            if (pattern.Match(line).Success)
                count += 1;
        }

        /// Return results of property search
        public bool endTest() => (count >= minimumMatch && count <= maximumMatch);

        /// Reconstruct the property from an XML tag
        public void restoreXml(Element el)
        {
            name = el.getAttributeValue("name");
            minimumMatch = int.Parse(el.getAttributeValue("min"));
            maximumMatch = int.Parse(el.getAttributeValue("max"));
            pattern = new Regex(el.getContent());
        }
    }
}
