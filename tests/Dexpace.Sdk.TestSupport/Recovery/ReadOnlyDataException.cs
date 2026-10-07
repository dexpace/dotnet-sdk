// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Collections;

namespace Dexpace.Sdk.TestSupport.Recovery;

/// <summary>An exception whose <see cref="Exception.Data"/> is read-only: any write throws <see cref="NotSupportedException"/>.</summary>
public sealed class ReadOnlyDataException : Exception
{
    private readonly ReadOnlyData _data = new();

    /// <inheritdoc />
    public override IDictionary Data => _data;

    private sealed class ReadOnlyData : IDictionary
    {
        public object? this[object key]
        {
            get => null;
            set => throw new NotSupportedException("Read-only.");
        }

        public ICollection Keys => Array.Empty<object>();

        public ICollection Values => Array.Empty<object>();

        public bool IsReadOnly => true;

        public bool IsFixedSize => true;

        public int Count => 0;

        public object SyncRoot => this;

        public bool IsSynchronized => false;

        public void Add(object key, object? value) => throw new NotSupportedException("Read-only.");

        public void Clear() => throw new NotSupportedException("Read-only.");

        public bool Contains(object key) => false;

        public void CopyTo(Array array, int index)
        {
        }

        public IDictionaryEnumerator GetEnumerator() => new Hashtable().GetEnumerator();

        public void Remove(object key) => throw new NotSupportedException("Read-only.");

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
