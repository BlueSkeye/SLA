﻿using Sla.CORE;

namespace Sla.DECCORE
{
    internal class OptionSplitDatatypes : ArchOption
    {
        [Flags()]
        public enum Options
        {
            /// Split combined structure fields
            option_struct = 1,
            /// Split combined array elements
            option_array = 2,
            /// Split combined LOAD and STORE operations
            option_pointer = 4
        }

        /// Translate option string to a configuration bit
        /// Possible value are:
        ///   - (empty string) = 0
        ///   - "struct"       = 1
        ///   - "array"        = 2
        ///   - "pointer"     = 4
        ///
        /// \param val is the option string
        /// \return the corresponding configuration bit
        public static Options getOptionBit(string val)
        {
            if (val.Length == 0) return 0;
            if (val == "struct") return Options.option_struct;
            if (val == "array") return Options.option_array;
            if (val == "pointer") return Options.option_pointer;
            throw new LowlevelError("Unknown data-type split option: " + val);
        }

        public OptionSplitDatatypes()
        {
            name = "splitdatatype";
        }

        public override string apply(Architecture glb, string p1, string p2, string p3)
        {
            Options oldConfig = glb.split_datatype_config;
            glb.split_datatype_config = getOptionBit(p1);
            glb.split_datatype_config |= getOptionBit(p2);
            glb.split_datatype_config |= getOptionBit(p3);

            if ((glb.split_datatype_config & (Options.option_struct | Options.option_array)) == 0) {
                glb.allacts.toggleAction(glb.allacts.getCurrentName(), "splitcopy", false);
                glb.allacts.toggleAction(glb.allacts.getCurrentName(), "splitpointer", false);
            }
            else {
                bool pointers = (glb.split_datatype_config & Options.option_pointer) != 0;
                glb.allacts.toggleAction(glb.allacts.getCurrentName(), "splitcopy", true);
                glb.allacts.toggleAction(glb.allacts.getCurrentName(), "splitpointer", pointers);
            }

            if (oldConfig == glb.split_datatype_config)
                return "Split data-type configuration unchanged";
            return "Split data-type configuration set";
        }
    }
}
