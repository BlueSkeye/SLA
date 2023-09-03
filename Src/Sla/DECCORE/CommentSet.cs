
namespace Sla.DECCORE
{
    internal class CommentSet : SortedSet<Comment>
    {
        internal CommentSet()
            :base(CommentOrder.Instance)
        {
        }
    }
}
