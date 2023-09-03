
namespace Sla.DECCORE
{
    /// \brief Compare two Comment pointers
    /// Comments are ordered first by function, then address,
    /// then the sub-sort index.
    internal class CommentOrder : IComparer<Comment>
    {
        internal static readonly CommentOrder Instance = new CommentOrder();

        /// Comparison operator
        /// \param a is the first Comment to compare
        /// \param b is the second
        /// \return \b true is the first is ordered before the second
        public int CompareTo(Comment a, Comment b)
        {
            if (a.getFuncAddr() != b.getFuncAddr()) {
                return a.getFuncAddr().CompareTo(b.getFuncAddr());
            }
            if (a.getAddr() != b.getAddr()) {
                return a.getAddr().CompareTo(b.getAddr());
            }
            if (a.getUniq() != b.getUniq()) {
                return a.getUniq().CompareTo(b.getUniq());
            }
            return 1;
        }
    }
}
