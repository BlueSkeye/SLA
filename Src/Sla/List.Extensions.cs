using System.Collections;

namespace Sla
{
    internal static partial class Extensions
    {
        internal static bool empty<T>(this List<T> from) => from.Count == 0;

        internal delegate T ResizeInstantiatorDelegate<T>();

        internal static void resize<T>(this List<T> list, int newSize,
            ResizeInstantiatorDelegate<T>? instantiator = null)
        {
            int currentCount = list.Count;
            if (currentCount > newSize) {
                list.Capacity = newSize;
                return;
            }
            if (currentCount == newSize) return;
            list.Capacity = newSize;
            for(int index = currentCount; index < newSize; index++) {
                list[index] = (null == instantiator) ? default(T) : instantiator();
            }
        }

        internal static int size<T>(this List<T> from) => from.Count;

        internal static IBiDirEnumerator<T> GetBiDirectionalEnumerator<T>(this List<T> from,
            bool reverseOrder = false)
            => new BiDirEnumerator<T>(from, reverseOrder);
        
        internal static T GetLastItem<T>(this List<T> from)
        {
            int lastItemIndex = from.Count - 1;

            if (lastItemIndex < 0) {
                throw new BugException();
            }
            return from[lastItemIndex];
        }

        internal static void SetLastItem<T>(this List<T> from, T newValue)
        {
            int lastItemIndex = from.Count - 1;

            if (lastItemIndex < 0) {
                throw new BugException();
            }
            from[lastItemIndex] = newValue;
            return;
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

        private class BiDirEnumerator<T> : IBiDirEnumerator<T>
        {
            private int _currentIndex;
            private List<T> _data;
            private bool _defaultIsReverse;
            private bool _disposed = false;
            private bool _reverseEnumerate;

            internal BiDirEnumerator(List<T> data, bool reverseOrder = false)
            {
                _currentIndex = reverseOrder ? data.Count : -1;
                _data = data;
                _defaultIsReverse = reverseOrder;
            }

            private void AssertNotDisposed()
            {
                if (_disposed) throw new ObjectDisposedException(GetType().Name);
            }

            public T Current
            {
                get
                {
                    AssertNotDisposed();
                    if (!IsPositionValid) throw new InvalidOperationException();
                    return _data[_currentIndex];
                }
            }

            public bool IsAfterLast
            {
                get
                {
                    AssertNotDisposed();
                    return (_data.Count <= _currentIndex);
                }
            }

            public bool IsBeforeFirst
            {
                get
                {
                    AssertNotDisposed();
                    return (0 > _currentIndex);
                }
            }

            public bool IsEnumeratingForward
            {
                get
                {
                    AssertNotDisposed();
                    return !_reverseEnumerate;
                }
            }

            public bool IsPositionValid
            {
                get
                {
                    AssertNotDisposed();
                    return (_currentIndex >= 0) && (_currentIndex < _data.Count);
                }
            }

            object IEnumerator.Current => this.Current;

            public void Dispose()
            {
                _disposed = true;
            }

            public bool MoveNext()
            {
                AssertNotDisposed();
                if (_reverseEnumerate) {
                    if (0 == _currentIndex) {
                        _currentIndex = -1;
                        return false;
                    }
                    if (0 > _currentIndex) {
                        return false;
                    }
                    _currentIndex--;
                    return true;
                }
                if ((_data.Count - 1) == _currentIndex) {
                    _currentIndex = _data.Count;
                    return false;
                }
                if (_data.Count <= _currentIndex) {
                    return false;
                }
                _currentIndex++;
                return true;
            }

            public bool MovePrevious()
            {
                AssertNotDisposed();
                if (!_reverseEnumerate) {
                    if (0 == _currentIndex) {
                        _currentIndex = -1;
                        return false;
                    }
                    if (0 > _currentIndex) {
                        return false;
                    }
                    _currentIndex--;
                    return true;
                }
                if ((_data.Count - 1) == _currentIndex) {
                    _currentIndex = _data.Count;
                    return false;
                }
                if (_data.Count <= _currentIndex) {
                    return false;
                }
                _currentIndex++;
                return true;
            }

            public void Reset()
            {
                AssertNotDisposed();
                _reverseEnumerate = _defaultIsReverse;
            }

            public void ReverseEnumDirection()
            {
                AssertNotDisposed();
                _reverseEnumerate = !_reverseEnumerate;
            }
        }
    }
}
