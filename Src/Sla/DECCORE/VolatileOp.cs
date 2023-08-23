using Sla.CORE;

namespace Sla.DECCORE
{
    /// \brief A base class for operations that access volatile memory
    ///
    /// The decompiler models volatile memory by converting any direct read or write of
    /// the memory to a function that \e accesses the memory. This class and its derived
    /// classes model such functions. Within the p-code control-flow, dedicated user defined
    /// ops serve as a placeholder for the (possibly unknown) effects of modifying/accessing the
    /// memory and prevent accidental constant propagation.
    internal class VolatileOp : UserPcodeOp
    {
        /// Append a suffix to a string encoding a specific size
        /// This allows a single user defined operator to have multiple symbol names
        /// based on the size of its operands in context.
        /// \param base is the string to append the suffix to
        /// \param size is the size to encode expressed as the number of bytes
        /// \return the appended string
        protected static string appendSize(string @base, int size)
        {
            if (size == 1)
                return @base + "_1";
            if (size == 2)
                return @base + "_2";
            if (size == 4)
                return @base + "_4";
            if (size == 8)
                return @base + "_8";
            TextWriter s = new StringWriter();
            s.Write($"{@base}_{size}");
            return s.ToString();
        }

        public VolatileOp(Architecture g, string nm,int ind)
            : base(g, nm, ind)
        {
        }

        /// Currently volatile ops only need their name
        public override void decode(Sla.CORE.Decoder decoder)
        {
        }
    }
}
