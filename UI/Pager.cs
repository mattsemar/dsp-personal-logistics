using System;
using System.Collections.Generic;

namespace PersonalLogistics.UI
{
    internal class Pager<T>
    {
        public int PageNum = 0;

        public int PageSize = 25;

        public List<T> _items;

        public Pager(List<T> items, int pageSize = 10)
        {
            PageSize = pageSize;
            _items = new List<T>(items);
        }

        public int Count => _items.Count;

        public void Reset()
        {
            PageNum = 0;
        }

        public void Next()
        {
            PageNum++;
        }

        public bool IsFirst()
        {
            return PageNum == 0;
        }

        public (int startIndex, int endIndex) GetIndexes()
        {
            var beginNdx = PageNum * PageSize;
            return (PageNum * PageSize, Math.Min(beginNdx + PageSize, _items.Count));
        }

        public List<T> GetPage()
        {
            var (startIndex, endIndex) = GetIndexes();
            return _items.GetRange(startIndex, endIndex - startIndex);
        }

        public bool HasNext()
        {
            return _items.Count > GetIndexes().endIndex + 1;
        }

        public bool IsEmpty()
        {
            return _items.Count == 0;
        }

        public void Previous()
        {
            PageNum--;
        }
    }
}