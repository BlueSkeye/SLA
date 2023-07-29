using Sla.CORE;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.SLEIGH
{
    /// \brief class for recording source file information for SLEIGH constructors.

    ///
    /// A SLEIGH specification may contain many source files.  This class is
    /// used to associate each constructor in a SLEIGH language to the source
    /// file where it is defined. This information is useful when debugging
    /// SLEIGH specifications.  Sourcefiles are assigned a numeric index and
    /// the mapping from indices to filenames is written to the generated .sla
    /// file.  For each constructor, the data written to the .sla file includes
    /// the source file index.
    internal class SourceFileIndexer
    {
        public SourceFileIndexer()
        {
            leastUnusedIndex = 0;
        }
        
        ~SourceFileIndexer()
        {
        }
        
        ///Returns the index of the file.  If the file is not in the index it is added.
        public int4 index(string filename)
        {
            auto it = fileToIndex.find(filename);
            if (fileToIndex.end() != it)
            {
                return it.second;
            }
            fileToIndex[filename] = leastUnusedIndex;
            indexToFile[leastUnusedIndex] = filename;
            return leastUnusedIndex++;
        }

        /// get the index of a file.  Error if the file is not in the index.
        public int4 getIndex(string filename)
        {
            return fileToIndex[filename];
        }

        /// get the filename corresponding to an index
        public string getFilename(int4 index)
        {
            return indexToFile[index];
        }

        /// read a stored index mapping from an XML file
        public void restoreXml(Element el)
        {
            List sourceFiles = el.getChildren();
            List::const_iterator iter = sourceFiles.begin();
            for (; iter != sourceFiles.end(); ++iter)
            {
                string filename = (*iter).getAttributeValue("name");
                int4 index = stoi((*iter).getAttributeValue("index"), NULL, 10);
                fileToIndex[filename] = index;
                indexToFile[index] = filename;
            }
        }

        ///< save the index mapping to an XML file
        public void saveXml(TextWriter s)
        {
            s << "<sourcefiles>\n";
            for (int4 i = 0; i < leastUnusedIndex; ++i)
            {
                s << ("<sourcefile name=\"");
                string str = indexToFile.at(i).c_str();
                xml_escape(s, str);
                s << "\" index=\"" << dec << i << "\"/>\n";
            }
            s << "</sourcefiles>\n";
        }

        private int4 leastUnusedIndex; ///< one-up count for assigning indices to files
        private Dictionary<int4, string> indexToFile;  ///< map from indices to files
        private Dictionary<string, int4> fileToIndex;  ///< map from files to indices
    }
}
