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
        private int4 size;          ///< Size of address bus in bits
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
        public void decode(Decoder decoder)
        {
            uint4 elemId = decoder.openElement(ELEM_LANGUAGE);
            processor = decoder.readString(ATTRIB_PROCESSOR);
            isbigendian = (decoder.readString(ATTRIB_ENDIAN) == "big");
            size = decoder.readSignedInteger(ATTRIB_SIZE);
            variant = decoder.readString(ATTRIB_VARIANT);
            version = decoder.readString(ATTRIB_VERSION);
            slafile = decoder.readString(ATTRIB_SLAFILE);
            processorspec = decoder.readString(ATTRIB_PROCESSORSPEC);
            id = decoder.readString(ATTRIB_ID);
            deprecated = false;
            for (; ; )
            {
                uint4 attribId = decoder.getNextAttributeId();
                if (attribId == 0) break;
                if (attribId == ATTRIB_DEPRECATED)
                    deprecated = decoder.readBool();
            }
            for (; ; )
            {
                uint4 subId = decoder.peekElement();
                if (subId == 0) break;
                if (subId == ELEM_DESCRIPTION)
                {
                    decoder.openElement();
                    description = decoder.readString(ATTRIB_CONTENT);
                    decoder.closeElement(subId);
                }
                else if (subId == ELEM_COMPILER)
                {
                    compilers.emplace_back();
                    compilers.back().decode(decoder);
                }
                else if (subId == ELEM_TRUNCATE_SPACE)
                {
                    truncations.emplace_back();
                    truncations.back().decode(decoder);
                }
                else
                {   // Ignore other child elements
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
        public int4 getSize() => size;

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
            int4 defaultind = -1;
            for (int4 i = 0; i < compilers.size(); ++i)
            {
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
        public int4 numCompilers() => compilers.size();

        /// Get the i-th compiler record
        public CompilerTag getCompiler(int4 i) => compilers[i] ;

        /// Get the number of truncation records
        public int4 numTruncations() => truncations.size();

        /// Get the i-th truncation record
        public TruncationTag getTruncation(int4 i) => truncations[i] ;
    }
}
