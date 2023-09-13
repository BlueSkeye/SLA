namespace Sla.EXTRA
{
    internal interface IRecordTypeInstanciator<recordtype, inittype, linetype>
    {
        recordtype CreateRecord(inittype initdata, linetype a, linetype b);
    }
}
