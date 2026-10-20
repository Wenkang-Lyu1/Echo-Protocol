using System.Collections.Generic;

// ============================================================
// DataContext -- one small bag of values per View.
//
// Used to hand data down the view tree when one View needs something the
// other already has:
//
//     SetData("city", cityData);          // parent stores it
//     GetData<CityData>("city");          // any child reads it (walks up)
//
// Keys are plain strings, values are plain objects; there is no schema to
// learn. Use it for prototype wiring, not as a second data model.
// ============================================================
public class DataContext
{
    private readonly Dictionary<string, object> _values = new Dictionary<string, object>();

    public bool Has(string key) => _values.ContainsKey(key);

    public T Get<T>(string key) where T : class
    {
        return _values.TryGetValue(key, out var value) ? value as T : null;
    }

    public void Set(string key, object value)
    {
        if (value == null) _values.Remove(key);
        else _values[key] = value;
    }

    public void Clear() => _values.Clear();
}
