
using System.Text;

namespace Sla
{
    internal static class TextReaderExtensions
    {
        /// <summary></summary>
        /// <param name="reader"></param>
        /// <returns>true if end of stream is reached.</returns>
        internal static bool ReadSpaces(this TextReader reader)
        {
            while (true) {
                int candidate = reader.Peek();
                if (-1 == candidate) return true;
                if (!Char.IsWhiteSpace((char)candidate)) return false;
                reader.Read();
            }
        }

        internal static string ReadString(this TextReader reader)
        {
            StringBuilder builder = new StringBuilder();
            while (true) {
                int candidate = reader.Peek();
                if (-1 == candidate) return builder.ToString();
                if (Char.IsWhiteSpace((char)candidate)) return builder.ToString();
                reader.Read();
            }
        }

        internal static long ReadDecimalUnsignedIntegerWithUnknownBase(this TextReader reader)
        {
            throw new NotImplementedException();
        }
    }
}
