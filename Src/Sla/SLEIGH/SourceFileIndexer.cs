using Sla.CORE;

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
        public int index(string filename)
        {
            int index;
            if (fileToIndex.TryGetValue(filename, out index)) {
                return index;
            }
            fileToIndex[filename] = leastUnusedIndex;
            indexToFile[leastUnusedIndex] = filename;
            return leastUnusedIndex++;
        }

        /// get the index of a file.  Error if the file is not in the index.
        public int getIndex(string filename)
        {
            return fileToIndex[filename];
        }

        /// get the filename corresponding to an index
        public string getFilename(int index)
        {
            return indexToFile[index];
        }

        /// read a stored index mapping from an XML file
        public void restoreXml(Element el)
        {
            IEnumerator<Element> iter = el.getChildren().GetEnumerator();
            while (iter.MoveNext()) {
                string filename = iter.Current.getAttributeValue("name");
                int index = stoi(iter.Current.getAttributeValue("index"), NULL, 10);
                fileToIndex[filename] = index;
                indexToFile[index] = filename;
            }
        }

        ///< save the index mapping to an XML file
        public void saveXml(TextWriter s)
        {
            s.WriteLine("<sourcefiles>");
            for (int i = 0; i < leastUnusedIndex; ++i) {
                s.Write("<sourcefile name=\"");
                string str = indexToFile[i].ToString();
                Xml.xml_escape(s, str);
                s.WriteLine($"\" index=\"{i}\"/>");
            }
            s.WriteLine("</sourcefiles>");
        }

        private int leastUnusedIndex; ///< one-up count for assigning indices to files
        private Dictionary<int, string> indexToFile;  ///< map from indices to files
        private Dictionary<string, int> fileToIndex;  ///< map from files to indices
    }
}
