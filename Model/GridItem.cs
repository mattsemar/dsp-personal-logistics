using System.Collections.Generic;
using System.IO;
using PersonalLogistics.Util;

namespace PersonalLogistics.Model
{
    public class GridItem
    {
        private const int VERSION = 2;
        public int Index;
        public int ItemId;
        public int Count;
        public int ProliferatorPoints;

        public override string ToString() => $"GridItem: index={Index}, itemId={ItemId}, count={Count}";

        private sealed class IndexItemIdCountEqualityComparer : IEqualityComparer<GridItem>
        {
            public bool Equals(GridItem x, GridItem y)
            {
                if (ReferenceEquals(x, y))
                {
                    return true;
                }

                if (ReferenceEquals(x, null))
                {
                    return false;
                }

                if (ReferenceEquals(y, null))
                {
                    return false;
                }

                if (x.GetType() != y.GetType())
                {
                    return false;
                }

                return x.Index == y.Index && x.ItemId == y.ItemId && x.Count == y.Count;
            }

            public int GetHashCode(GridItem obj)
            {
                unchecked
                {
                    var hashCode = obj.Index;
                    hashCode = (hashCode * 397) ^ obj.ItemId;
                    hashCode = (hashCode * 397) ^ obj.Count;
                    return hashCode;
                }
            }
        }

        public static IEqualityComparer<GridItem> IndexItemIdCountComparer { get; } = new IndexItemIdCountEqualityComparer();

        private bool Equals(GridItem other) => Index == other.Index && ItemId == other.ItemId && Count == other.Count;

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
            {
                return false;
            }

            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            if (obj.GetType() != this.GetType())
            {
                return false;
            }

            return Equals((GridItem)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = Index;
                hashCode = (hashCode * 397) ^ ItemId;
                hashCode = (hashCode * 397) ^ Count;
                hashCode = (hashCode * 397) ^ ProliferatorPoints;
                return hashCode;
            }
        }

        public static GridItem From(int index, int itemId, int count, int inc)
        {
            return new GridItem
            {
                Index = index,
                ItemId = itemId,
                Count = count,
                ProliferatorPoints = inc
            };
        }

        public static GridItem Import(BinaryReader r)
        {
            var version = r.ReadInt32();
            if (version != VERSION)
            {
                Log.Warn($"version mismatch on GridItem {VERSION} vs {version}");
            }

            var result = new GridItem()
            {
                Index = r.ReadInt32(),
                ItemId = r.ReadInt32(),
                Count = r.ReadInt32()
            };
            if (version > 1)
            {
                result.ProliferatorPoints = r.ReadInt32();
            }
            return result;
        }

        public void Export(BinaryWriter w)
        {
            w.Write(VERSION);
            w.Write(Index);
            w.Write(ItemId);
            w.Write(Count);
            w.Write(ProliferatorPoints);
        }
    }
}