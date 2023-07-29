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
            if (dcp.conf == (Architecture*)0)
                throw IfaceExecutionError("No load image present");
            Address addr = parse_machaddr(s, size, *dcp.conf.types); // Read storage location
            if (size == 0)
                throw IfaceExecutionError("Must specify a size");
            int split = -1;

            s >> ws;
            if (s.eof())
                throw IfaceParseError("Missing split offset");
            s >> dec >> split;
            if (split == -1)
                throw IfaceParseError("Bad split offset");
            dcp.conf.splitrecords.emplace_back();
            PreferSplitRecord & rec(dcp.conf.splitrecords.back());

            rec.storage.space = addr.getSpace();
            rec.storage.offset = addr.getOffset();
            rec.storage.size = size;
            rec.splitoffset = split;

            *status.optr << "Successfully added split record" << endl;
        }
    }
}
