
using System.Text;

namespace Sla
{
    internal static class TextReaderExtensions
    {
        internal static bool EofReached(this TextReader reader)
        {
            return (-1 != reader.Peek());
        }
        
        internal static char? ReadCharacter(this TextReader reader)
        {
            int candidate = reader.Peek();
            if (-1 == candidate) return null;
            reader.Read();
            return (char)candidate;
        }

        internal static char ReadMandatoryCharacter(this TextReader reader)
        {
            return reader.ReadCharacter() ?? throw new ApplicationException();
        }

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

        internal enum KnownBase
        {
            Octal,
            Decimal,
            Hexadecimal
        }

        internal static uint ReadDecimalUnsignedIntegerWithKnownBase(this TextReader reader,
            KnownBase @base)
        {
            throw new NotImplementedException();
        }

        internal static uint ReadDecimalUnsignedIntegerWithUnknownBase(this TextReader reader)
        {
            throw new NotImplementedException();
        }

        internal static int ReadDecimalIntegerWithUnknownBase(this TextReader reader)
        {
            throw new NotImplementedException();
        }
    }
}
