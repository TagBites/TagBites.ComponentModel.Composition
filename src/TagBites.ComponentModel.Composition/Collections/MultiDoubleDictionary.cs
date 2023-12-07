using System.Collections.Generic;
using System.Linq;

namespace TagBites.Collections;

internal class MultiDoubleDictionary<TKeyFirst, TKeySecond, TCollection, TValue> : DoubleDictionary<TKeyFirst, TKeySecond, TCollection>
    where TCollection : ICollection<TValue>, new()
{
    public bool ContainsValue(TValue value)
    {
        return Values.Any(collection => collection.Contains(value));
    }
    public bool ContainsValue(TKeyFirst firstKey, TKeySecond secondKey, TValue value)
    {
        var collection = TryGetValueDefault(firstKey, secondKey);
        if (collection != null)
            return collection.Contains(value);

        return false;
    }

    public void Add(TKeyFirst firstKey, TKeySecond secondKey, TValue value)
    {
        var collection = TryGetValueDefault(firstKey, secondKey);
        if (collection == null)
        {
            collection = new TCollection();
            Set(firstKey, secondKey, collection);
        }

        collection.Add(value);
    }
    public bool Remove(TKeyFirst firstKey, TKeySecond secondKey, TValue value)
    {
        var collection = TryGetValueDefault(firstKey, secondKey);
        if (collection != null)
            return collection.Remove(value);

        return false;
    }

    public int GetValueCount()
    {
        var count = 0;

        foreach (var collection in Values)
            count += collection.Count;

        return count;
    }
    public int GetValueCount(TKeyFirst firstKey, TKeySecond secondKey)
    {
        var collection = TryGetValueDefault(firstKey, secondKey);
        if (collection != null)
            return collection.Count;

        return 0;
    }
}
