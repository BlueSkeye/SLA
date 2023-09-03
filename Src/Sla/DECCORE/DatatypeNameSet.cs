
namespace Sla.DECCORE
{
    // A set of data-types sorted by name
    // sorted by DatatypeNameCompare
    internal class DatatypeNameSet : SortedSet<Datatype>
    {
        internal DatatypeNameSet()
            : base(DatatypeNameCompare.Instance)
        {
        }
    }
}
