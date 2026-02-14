using System;
using System.Collections;
using System.Collections.Generic;

namespace Nexum.SourceGenerators
{
    /// <summary>
    /// An immutable array wrapper that implements value equality for use in
    /// Roslyn incremental generator pipelines (caching requires value equality).
    /// </summary>
    internal readonly struct EquatableArray<T> : IEquatable<EquatableArray<T>>, IEnumerable<T>
        where T : IEquatable<T>
    {
        private readonly T[]? _array;

        public EquatableArray(T[] array)
        {
            _array = array;
        }

        public int Length => _array?.Length ?? 0;

        public T this[int index] => _array![index];

        public bool Equals(EquatableArray<T> other)
        {
            if (_array is null && other._array is null)
            {
                return true;
            }

            if (_array is null || other._array is null)
            {
                return false;
            }

            if (_array.Length != other._array.Length)
            {
                return false;
            }

            for (int i = 0; i < _array.Length; i++)
            {
                if (!_array[i].Equals(other._array[i]))
                {
                    return false;
                }
            }

            return true;
        }

        public override bool Equals(object? obj)
        {
            return obj is EquatableArray<T> other && Equals(other);
        }

        public override int GetHashCode()
        {
            if (_array is null)
            {
                return 0;
            }

            unchecked
            {
                int hash = 17;
                foreach (T item in _array)
                {
                    hash = (hash * 31) + item.GetHashCode();
                }
                return hash;
            }
        }

        public static bool operator ==(EquatableArray<T> left, EquatableArray<T> right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(EquatableArray<T> left, EquatableArray<T> right)
        {
            return !left.Equals(right);
        }

        public IEnumerator<T> GetEnumerator()
        {
            if (_array is null)
            {
                yield break;
            }

            foreach (T item in _array)
            {
                yield return item;
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
