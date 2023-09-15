
namespace Sla.DECCORE
{
    /// \brief Node for a forward traversal of a Varnode expression
    internal class TraverseNode
    {
        [Flags()]
        internal enum Flags
        {
            /// Alternate path traverses a solid action or \e non-incidental COPY
            actionalt = 1,
            /// Main path traverses an INDIRECT
            indirect = 2,
            /// Alternate path traverses an INDIRECT
            indirectalt = 4,
            /// Least significant byte(s) of original value have been truncated
            lsb_truncated = 8,
            /// Original value has been concatented as \e most significant portion
            concat_high = 0x10
        }
        /// Varnode at the point of traversal
        internal readonly Varnode vn;
        /// Flags associated with the node
        internal Flags flags;
        
        internal TraverseNode(Varnode v, Flags f)
        {
            vn = v;
            flags = f;
        }

        /// \brief Return \b true if the alternate path looks more valid than the main path.
        ///
        /// Two different paths from a common Varnode each terminate at a CALL, CALLIND, or RETURN.
        /// Evaluate which path most likely represents actual parameter/return value passing,
        /// based on traversal information about each path.
        /// \param vn is the Varnode terminating the \e alternate path
        /// \param flags indicates traversals for both paths
        /// \return \b true if the alternate path is preferred
        internal static bool isAlternatePathValid(Varnode vn, Flags flags)
        {
            if ((flags & (Flags.indirect | Flags.indirectalt)) == Flags.indirect)
                // If main path traversed an INDIRECT but the alternate did not
                // Main path traversed INDIRECT, alternate did not
                return true;
            if ((flags & (Flags.indirect | Flags.indirectalt)) == Flags.indirectalt)
                // Alternate path traversed INDIRECT, main did not
                return false;
            if ((flags & Flags.actionalt) != 0)
                // Alternate path traversed a dedicated COPY
                return true;
            if (vn.loneDescend() == (PcodeOp)null)
                return false;
            PcodeOp op = vn.getDef();
            if (op == (PcodeOp)null)
                return true;
            // MULTIEQUAL or INDIRECT indicates multiple values
            return !op.isMarker();
        }
    }
}
