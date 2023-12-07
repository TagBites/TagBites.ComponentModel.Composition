using System;
using System.Collections.Generic;
using System.Linq;

namespace TagBites.Collections;

internal class DoubleDictionary<TKeyFirst, TKeySecond, TValue> : IEnumerable<KeyValuePair<Tuple<TKeyFirst, TKeySecond>, TValue>>
{
    private readonly DoubleDictionary<TKeyFirst, TKeySecond, TValue> _dictionary;
    private readonly Dictionary<TKeyFirst, Dictionary<TKeySecond, TValue>> _collection = new();

    public IEnumerable<Tuple<TKeyFirst, TKeySecond>> Keys
    {
        get
        {
            foreach (var kvp in _collection)
                foreach (var kSecond in kvp.Value.Keys)
                    yield return Tuple.Create(kvp.Key, kSecond);
        }
    }
    public IEnumerable<TValue> Values
    {
        get
        {
            foreach (var d in _collection.Values)
                foreach (var v in d.Values)
                    yield return v;
        }
    }
    public int Count
    {
        get
        {
            var count = 0;
            foreach (var item in _collection)
                count += item.Value.Count;

            return count;
        }
    }
    public bool IsReadOnly => false;

    public TValue this[TKeyFirst keyFirst, TKeySecond keySecond]
    {
        get => _collection[keyFirst][keySecond];
        set
        {
            var second = GetSecond(keyFirst);
            if (second == null)
                _collection[keyFirst] = new Dictionary<TKeySecond, TValue>() { { keySecond, value } };
            else
                second[keySecond] = value;
        }
    }

    public DoubleDictionary()
    { }
    public DoubleDictionary(DoubleDictionary<TKeyFirst, TKeySecond, TValue> dictionary)
    {
        _dictionary = dictionary;

        foreach (var item in dictionary)
            this[item.Key.Item1, item.Key.Item2] = item.Value;
    }


    public void Add(TKeyFirst keyFirst, TKeySecond keySecond, TValue value)
    {
        var second = GetSecond(keyFirst);
        if (second == null)
            _collection[keyFirst] = second = new Dictionary<TKeySecond, TValue>() { { keySecond, value } };

        second.Add(keySecond, value);
    }
    public bool Set(TKeyFirst keyFirst, TKeySecond keySecond, TValue value)
    {
        var second = GetSecond(keyFirst);
        if (second == null)
        {
            _collection[keyFirst] = new Dictionary<TKeySecond, TValue>() { { keySecond, value } };
            return true;
        }

        var isNew = !second.ContainsKey(keySecond);
        second[keySecond] = value;
        return isNew;
    }

    public bool Remove(TKeyFirst keyFirst)
    {
        return _collection.Remove(keyFirst);
    }
    public bool Remove(TKeyFirst keyFirst, TKeySecond keySecond)
    {
        var second = GetSecond(keyFirst);
        return second != null && second.Remove(keySecond);
    }
    public void Clear()
    {
        _collection.Clear();
    }

    public bool ContainsKey(TKeyFirst keyFirst)
    {
        return _collection.ContainsKey(keyFirst);
    }
    public bool ContainsKey(TKeyFirst keyFirst, TKeySecond keySecond)
    {
        var second = GetSecond(keyFirst);
        return second != null && second.ContainsKey(keySecond);
    }
    public bool TryGetValue(TKeyFirst keyFirst, TKeySecond keySecond, out TValue value)
    {
        value = default(TValue);
        var second = GetSecond(keyFirst);

        return second != null && second.TryGetValue(keySecond, out value);
    }
    public TValue TryGetValueDefault(TKeyFirst keyFirst, TKeySecond keySecond, TValue defaultValue = default(TValue))
    {
        return TryGetValue(keyFirst, keySecond, out var value)
            ? value
            : defaultValue;
    }

    private Dictionary<TKeySecond, TValue> GetSecond(TKeyFirst keyFirst)
    {
        _collection.TryGetValue(keyFirst, out var second);
        return second;
    }

    public IEnumerator<KeyValuePair<Tuple<TKeyFirst, TKeySecond>, TValue>> GetEnumerator()
    {
        return GetEnumerable().GetEnumerator();
    }
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    private IEnumerable<KeyValuePair<Tuple<TKeyFirst, TKeySecond>, TValue>> GetEnumerable()
    {
        foreach (var d in _collection)
            foreach (var v in d.Value)
                yield return new KeyValuePair<Tuple<TKeyFirst, TKeySecond>, TValue>(
                    Tuple.Create(d.Key, v.Key),
                    v.Value);
    }

    public IEnumerable<TValue> GetValuesFor(TKeyFirst keyFirst)
    {
        var second = GetSecond(keyFirst);
        return second != null
            ? second.Values
            : Enumerable.Empty<TValue>();
    }
}
