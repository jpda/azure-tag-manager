using System.Collections.Generic;

namespace Azure.ExpirationHandler.Func
{
    public static class DictionaryExtensions
    {
        public static void AddOrUpdate<K, V>(this Dictionary<K, V> dict, K key, V val)
        {
            if (dict.ContainsKey(key))
            {
                dict[key] = val;
                return;
            }
            dict.Add(key, val);
        }

        public static void AddOrKeepExisting<K, V>(this Dictionary<K, V> dict, K key, V val)
        {
            if (dict.ContainsKey(key)) return;
            dict.Add(key, val);
        }
    }
}
