using System;
using System.Collections.Generic;

namespace PersonalLogistics.UI
{
    public class Pager<T>
    {
        public List<T> _items;
        public int PageNum;

        public int PageSize = 25;

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

        public bool IsFirst() => PageNum == 0;

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

        public bool HasNext() => _items.Count > GetIndexes().endIndex + 1;

        public bool IsEmpty() => _items.Count == 0;

        public void Previous()
        {
            PageNum--;
        }
    }
}