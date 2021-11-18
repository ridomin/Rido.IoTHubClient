using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace thermostat_sample
{
    public class FixedSizeDictonary<TKey, TValue> : Dictionary<TKey, TValue> where TKey : notnull
    {
        int size;
        Queue<TKey> orderedKeys = new Queue<TKey>();
        public FixedSizeDictonary(int maxSize) => size = maxSize;
        public new void Add(TKey key, TValue value)
        {
            orderedKeys.Enqueue(key);
            if (size != 0 && Count >= size) Remove(orderedKeys.Dequeue());
            base.Add(key, value);
        }
    }
}
