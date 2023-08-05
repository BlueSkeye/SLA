using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla
{
    internal static class Extensions
    {
        internal static bool empty<T>(this List<T> from) => from.Count == 0;

        internal static int size<T>(this List<T> from) => from.Count;

        internal static T GetLastItem<T>(this List<T> from)
        {
            int lastItemIndex = from.Count - 1;

            if (lastItemIndex < 0) {
                throw new BugException();
            }
            return from[lastItemIndex];
        }

        internal static IEnumerator<T> GetReverseEnumerator<T>(this List<T> from)
            => new ReverseEnumerator<T>(from);
 
        internal static void RemoveLastItem<T>(this List<T> from)
        {
            int lastItemIndex = from.Count - 1;

            if (0 > lastItemIndex) {
                throw new InvalidOperationException();
            }
            from.RemoveAt(lastItemIndex);
        }

        private class ReverseEnumerator<T> : IEnumerator<T>
        {
            private bool _disposed = false;
            private readonly List<T> from;
            private int index;

            internal ReverseEnumerator(List<T> from)
            {
                this.from = from;
                Reset();
            }

            public T Current
            {
                get
                {
                    AssertNotDisposed();
                    if (0 > index) {
                        throw new InvalidOperationException();
                    }
                    if (from.Count <= index) {
                        throw new InvalidOperationException();
                    }
                    return from[index];
                }
            }

            object IEnumerator.Current => this.Current;

            private void AssertNotDisposed()
            {
                if (_disposed) {
                    throw new ObjectDisposedException(GetType().Name);
                }
            }

            public void Dispose()
            {
                _disposed = true;
            }

            public bool MoveNext()
            {
                AssertNotDisposed();
                return (0 <= --index);
            }

            public void Reset()
            {
                AssertNotDisposed();
                index = from.Count;
            }
        }
    }
}
