using Sla.CORE;
using Sla.EXTRA;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    /// \brief Contents of the \<language> tag in a .ldefs file
    ///
    /// This class contains meta-data describing a single processor and the set of
    /// files used to analyze it.  Ghidra requires a compiled SLEIGH specification file
    /// (.sla), a processor specification file (.pspec), and a compiler specification file (.cspec)
    /// in order to support disassembly/decompilation of a processor.  This class supports
    /// a single processor, as described by a single SLEIGH file and processor spec.  Multiple
    /// compiler specifications can be given for the single processor.
    internal class LanguageDescription
    {
        private string processor;       ///< Name of processor
        private bool isbigendian;       ///< Set to \b true if this processor is \e big-endian
        private int size;          ///< Size of address bus in bits
        private string variant;     ///< Name of processor variant or "default"
        private string version;     ///< Version of the specification
        private string slafile;     ///< Name of .sla file for processor
        private string processorspec;       ///< Name of .pspec file
        private string id;          ///< Unique id for this language
        private string description;     ///< Human readable description of this language
        private bool deprecated;        ///< Set to \b true if the specification is considered \e deprecated
        private List<CompilerTag> compilers = new List<CompilerTag>();  ///< List of compiler specifications compatible with this processor
        private List<TruncationTag> truncations = new List<TruncationTag>();  ///< Address space truncations required by this processor
        
        public LanguageDescription()
        {
        }

        /// Parse \b this description from a stream
        /// Parse an ldefs \<language> element
        /// \param decoder is the stream decoder
        public void decode(Sla.CORE.Decoder decoder)
        {
            uint elemId = decoder.openElement(ElementId.ELEM_LANGUAGE);
            processor = decoder.readString(AttributeId.ATTRIB_PROCESSOR);
            isbigendian = (decoder.readString(AttributeId.ATTRIB_ENDIAN) == "big");
            size = (int)decoder.readSignedInteger(AttributeId.ATTRIB_SIZE);
            variant = decoder.readString(AttributeId.ATTRIB_VARIANT);
            version = decoder.readString(AttributeId.ATTRIB_VERSION);
            slafile = decoder.readString(AttributeId.ATTRIB_SLAFILE);
            processorspec = decoder.readString(AttributeId.ATTRIB_PROCESSORSPEC);
            id = decoder.readString(AttributeId.ATTRIB_ID);
            deprecated = false;
            while(true)
            {
                uint attribId = decoder.getNextAttributeId();
                if (attribId == 0) break;
                if (attribId == AttributeId.ATTRIB_DEPRECATED)
                    deprecated = decoder.readBool();
            }
            while(true) {
                uint subId = decoder.peekElement();
                if (subId == 0) break;
                if (subId == ElementId.ELEM_DESCRIPTION) {
                    decoder.openElement();
                    description = decoder.readString(AttributeId.ATTRIB_CONTENT);
                    decoder.closeElement(subId);
                }
                else if (subId == ElementId.ELEM_COMPILER) {
                    CompilerTag newTag = new CompilerTag();
                    newTag.decode(decoder);
                    compilers.Add(newTag);
                }
                else if (subId == ElementId.ELEM_TRUNCATE_SPACE) {
                    TruncationTag newTag = new TruncationTag();
                    newTag.decode(decoder);
                    truncations.Add(newTag);
                }
                else {
                    // Ignore other child elements
                    decoder.openElement();
                    decoder.closeElementSkipping(subId);
                }
            }
            decoder.closeElement(elemId);
        }

        /// Get the name of the processor
        public string getProcessor() => processor;

        /// Return \b true if the processor is big-endian
        public bool isBigEndian() => isbigendian;

        /// Get the size of the address bus
        public int getSize() => size;

        /// Get the processor variant
        public string getVariant() => variant;

        /// Get the processor version
        public string getVersion() => version;

        /// Get filename of the SLEIGH specification
        public string getSlaFile() => slafile;

        /// Get the filename of the processor specification
        public string getProcessorSpec() => processorspec;

        /// Get the \e language \e id string associated with this processor
        public string getId() => id;

        /// Get a description of the processor
        public string getDescription() => description;

        /// Return \b true if this specification is deprecated
        public bool isDeprecated() => deprecated;

        /// Get compiler specification of the given name
        /// Pick out the CompilerTag associated with the desired \e compiler \e id string
        /// \param nm is the desired id string
        /// \return a reference to the matching CompilerTag
        public CompilerTag getCompiler(string nm)
        {
            int defaultind = -1;
            for (int i = 0; i < compilers.Count; ++i) {
                if (compilers[i].getId() == nm)
                    return compilers[i];
                if (compilers[i].getId() == "default")
                    defaultind = i;
            }
            if (defaultind != -1)                 // If can't match compiler, return default
                return compilers[defaultind];
            return compilers[0];
        }

        /// Get the number of compiler records
        public int numCompilers() => compilers.Count;

        /// Get the i-th compiler record
        public CompilerTag getCompiler(int i) => compilers[i] ;

        /// Get the number of truncation records
        public int numTruncations() => truncations.Count;

        /// Get the i-th truncation record
        public TruncationTag getTruncation(int i) => truncations[i] ;
    }
}
