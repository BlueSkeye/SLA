using Sla.CORE;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    /// \brief A single property to be searched for in the output of a function decompilation
    ///
    /// This is generally a regular expression run over the characters in the
    /// decompiled "source" form of the function.
    /// The property may "match" more than once or not at all.
    internal class FunctionTestProperty
    {
        private int4 minimumMatch;      ///< Minimum number of times property is expected to match
        private int4 maximumMatch;      ///< Maximum number of times property is expected to match
        private string name;            ///< Name of the test, to be printed in test summaries
        private std::regex pattern;     ///< Regular expression to match against a line of output
        private /*mutable*/ uint4 count;		///< Number of times regular expression has been seen

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
            if (std::regex_search(line, pattern))
                count += 1;
        }

        /// Return results of property search
        public bool endTest() => (count >= minimumMatch && count <= maximumMatch);

        /// Reconstruct the property from an XML tag
        public void restoreXml(Element el)
        {
            name = el->getAttributeValue("name");
            istringstream s1(el->getAttributeValue("min"));
            s1 >> minimumMatch;
            istringstream s2(el->getAttributeValue("max"));
            s2 >> maximumMatch;
            pattern = std::regex(el->getContent());
        }
    }
}
