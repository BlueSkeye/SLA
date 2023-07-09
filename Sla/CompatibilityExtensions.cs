using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ghidra
{
    internal static class CompatibilityExtensions
    {
        private static void ConsumeDecimalDigits(this TextReader from, StringBuilder into)
        {
            while(true) {
                int rawCharacter = from.Peek();
                if (-1 == rawCharacter) {
                    break;
                }
                if (!char.IsDigit((char)rawCharacter)) {
                    break;
                }
                from.Read();
                into.Append((char)rawCharacter);
            }
        }

        internal static T BeforeUpperBound<T>(this SortedSet<T> from, T value,
            out bool first, out IEnumerator<T> next)
            where T : IComparable<T>
        {
            T? result = default(T);

            first = true;
            next = from.GetEnumerator();
            while (next.MoveNext()) {
                T t = next.Current;
                if (0 <= t.CompareTo(value)) {
                    return first ? t : result ?? throw new BugException();
                }
                result = t;
                first = false;
            }
            return result ?? throw new BugException();
        }

        internal static StreamReader get(this StreamReader from, byte[] into, int length,
            out int extractedCharactersCount, char delimiter = '\n')
        {
            if (null == into) {
                throw new ArgumentNullException();
            }
            if (0 > length) {
                throw new ArgumentException();
            }
            if (into.Length < length) {
                throw new ArgumentException();
            }
            extractedCharactersCount = 0;
            for (int index = 0; index < length; index++) {
                int candidate = from.Peek();
                if (-1 == candidate) {
                    break;
                }
                if (delimiter == (char)candidate) {
                    break;
                }
                extractedCharactersCount++;
                if (candidate > 0xFF) {
                    throw new BugException();
                }
                into[index] = (byte)candidate;
                from.Read();
            }
            return from;
        }
        
        internal static int ReadDecimalInteger(this TextReader from)
        {
            StringBuilder buffer = new StringBuilder();

            int rawCharacter = from.Peek();
            switch (rawCharacter) {
                case '+':
                case '-':
                    buffer.Append((char)rawCharacter);
                    from.Read();
                    break;
            }
            from.ConsumeDecimalDigits(buffer);
            return (0 == buffer.Length)
                ? 0
                : int.Parse(buffer.ToString());
        }
        
        internal static long ReadDecimalLong(this TextReader from)
        {
            StringBuilder buffer = new StringBuilder();

            int rawCharacter = from.Peek();
            switch (rawCharacter) {
                case '+':
                case '-':
                    buffer.Append((char)rawCharacter);
                    from.Read();
                    break;
            }
            from.ConsumeDecimalDigits(buffer);
            return (0 == buffer.Length)
                ? 0
                : long.Parse(buffer.ToString());
        }

        internal static uint ReadDecimalUnsignedInteger(this TextReader from)
        {
            StringBuilder buffer = new StringBuilder();

            from.ConsumeDecimalDigits(buffer);
            return (0 == buffer.Length) 
                ? 0
                : uint.Parse(buffer.ToString());
        }

        internal static ulong ReadDecimalUnsignedLongInteger(this TextReader from)
        {
            StringBuilder buffer = new StringBuilder();

            from.ConsumeDecimalDigits(buffer);
            return (0 == buffer.Length) 
                ? 0
                : ulong.Parse(buffer.ToString());
        }
    }
}
