namespace Sla.EXTRA
{
    internal interface IRangemapSubsortTypeInstantiator<T>
    {
        T Create(bool value);
        
        T Create(T cloned);
    }
}
