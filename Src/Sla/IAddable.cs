
namespace Sla
{
    internal interface IAddable<T>
    {
        T IncrementBy(T initialValue, int incrementBy);

        T DecrementBy(T initialValue, int decrementBy);
    }
}
