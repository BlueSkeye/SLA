using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    internal class OptionAliasBlock : ArchOption
    {
        public OptionAliasBlock()
        {
            name = "aliasblock";
        }

        /// \class OptionAliasBlock
        /// \brief Set how locked data-types on the stack affect alias heuristics
        ///
        /// Stack analysis uses the following simple heuristic: a pointer is unlikely to reference (alias)
        /// a stack location if there is a locked data-type between the pointer base and the location.
        /// This option determines what kind of locked data-types \b block aliases in this way.
        ///   - none - no data-types will block an alias
        ///   - struct - only structure data-types will block an alias
        ///   - array - array data-types (and structure data-types) will block an alias
        ///   - all - all locked data-types will block an alias
        public override string apply(Architecture glb, string p1, string p2, string p3)
        {
            if (p1.size() == 0)
                throw ParseError("Must specify alias block level");
            int4 oldVal = glb.alias_block_level;
            if (p1 == "none")
                glb.alias_block_level = 0;
            else if (p1 == "struct")
                glb.alias_block_level = 1;
            else if (p1 == "array")
                glb.alias_block_level = 2;     // The default. Let structs and arrays block aliases
            else if (p1 == "all")
                glb.alias_block_level = 3;
            else
                throw ParseError("Unknown alias block level: " + p1);
            if (oldVal == glb.alias_block_level)
                return "Alias block level unchanged";
            return "Alias block level set to " + p1;
        }
    }
}
