#nullable enable
using System;
using System.Collections.Generic;

// The data model uses C# 9 records, but never Unity serialization.
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
}

namespace ProceduralGeneration
{
    internal static class UnityCompatibility
    {
        // Enumerable.MaxBy is absent in Unity's .NET Standard 2.1 profile.
        // Like the original, retain the first item when keys compare equal.
        internal static T MaxByStable<T, TKey>(this IEnumerable<T> source, Func<T, TKey> selector)
        {
            using var iterator = source.GetEnumerator();
            if (!iterator.MoveNext()) throw new InvalidOperationException("Sequence contains no elements.");
            T best = iterator.Current;
            TKey key = selector(best);
            var comparer = Comparer<TKey>.Default;
            while (iterator.MoveNext())
            {
                TKey candidate = selector(iterator.Current);
                if (comparer.Compare(candidate, key) <= 0) continue;
                best = iterator.Current;
                key = candidate;
            }
            return best;
        }
    }
}
