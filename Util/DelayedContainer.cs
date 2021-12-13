using System;
using System.Collections.Generic;

namespace PersonalLogistics.Util
{
    public class DelayedContainer<T>
    {
        private readonly List<T> _items = new List<T>();

        private readonly Dictionary<T, DateTime> _addedAt = new Dictionary<T, DateTime>();

        // only items that have been in this long are returned
        private TimeSpan _minAge;
        
        public DelayedContainer(TimeSpan age)
        {
            _minAge = age;
        }

        private bool RemoveItem(T item)
        {
            _addedAt.Remove(item);
            return _items.Remove(item);
        }

        public void AddItems(params T[] items)
        {
            foreach (var newItem in items)
            {
                _items.Add(newItem);
                _addedAt[newItem] = DateTime.Now;
            }
        }

        private List<T> GetAvailableItems()
        {
            var result = _items.FindAll(i => AgeOf(i) > _minAge);
            result.Sort((i1, i2) => AgeOf(i2).CompareTo(AgeOf(i1)));
            return result;
        }

        public object PopAvailableItem()
        {
            if (_items.Count == 0)
                return null;
            var availableItems = GetAvailableItems();
            if (availableItems.Count == 0)
                return null;
            T oldest = availableItems[0];

            if (!RemoveItem(oldest))
            {
                Log.Warn($"item {oldest} not removed");
            }

            return oldest;
        }

        private TimeSpan AgeOf(T item)
        {
            if (_addedAt.ContainsKey(item))
            {
                return DateTime.Now - _addedAt[item];
            }

            Log.Warn($"Somehow this item {item} does not have added at value");
            return TimeSpan.MaxValue;
        }

        public bool HasItem(T item)
        {
            return _addedAt.ContainsKey(item);
        }

        public int MinAgeSeconds() => (int)_minAge.TotalSeconds;

        public void UpdateMinAgeSeconds(int seconds)
        {
            _minAge = TimeSpan.FromSeconds(seconds);
        }
    }
}