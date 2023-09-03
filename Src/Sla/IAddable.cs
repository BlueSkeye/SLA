
namespace Sla
{
    internal interface IAddable<T>
    {
        T IncrementBy(int incrementBy);

        T DecrementBy(int decrementBy);
    }
}
