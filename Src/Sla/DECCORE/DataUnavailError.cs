
namespace Sla.DECCORE
{
    /// \brief Exception indicating data was not available
    ///
    /// This exception is thrown when a request for load image
    /// data cannot be met, usually because the requested address
    /// range is not in the image.
    internal class DataUnavailError : Sla.CORE.LowlevelError
    {
        /// Instantiate with an explanatory string
        internal DataUnavailError(string s)
            : base(s)
        {
        }
    }
}
