
namespace Sla.DECCORE
{
    /// A set of data-types sorted by function
    // sorted by DatatypeCompare
    internal class DatatypeSet : SortedSet<Datatype>
    {
        internal DatatypeSet()
            : base(DatatypeCompare.Instance)
        {
        }
    }
}
