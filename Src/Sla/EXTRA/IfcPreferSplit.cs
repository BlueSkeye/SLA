using Sla.CORE;
using Sla.DECCORE;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
    internal class IfcPreferSplit : IfaceDecompCommand
    {
        /// \class IfcPreferSplit
        /// \brief Mark a storage location to be split: `prefersplit <address+size> <splitsize>`
        ///
        /// The storage location is marked for splitting in any future decompilation.
        /// During decompilation, any Varnode matching the storage location on the command-line
        /// will be generally split into two pieces, where the final command-line parameter
        /// indicates the number of bytes in the first piece.  A Varnode is split only if operations
        /// involving it can also be split.  See PreferSplitManager.
        public override void execute(TextReader s)
        {
            int size = 0;
            if (dcp.conf == (Architecture)null)
                throw new IfaceExecutionError("No load image present");
            Address addr = Grammar.parse_machaddr(s, out size, dcp.conf.types); // Read storage location
            if (size == 0)
                throw new IfaceExecutionError("Must specify a size");
            int split = -1;

            s.ReadSpaces();
            if (s.EofReached())
                throw new IfaceParseError("Missing split offset");
            if (!int.TryParse(s.ReadString(), out split))
                throw new IfaceParseError("Bad split offset");
            PreferSplitRecord rec = new PreferSplitRecord();
            dcp.conf.splitrecords.Add(rec);

            rec.storage.space = addr.getSpace();
            rec.storage.offset = addr.getOffset();
            rec.storage.size = size;
            rec.splitoffset = split;

            status.optr.WriteLine("Successfully added split record");
        }
    }
}
