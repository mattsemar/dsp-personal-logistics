using System;
using System.Text;
using PersonalLogistics.Util;

namespace PersonalLogistics.Model
{
    public class ItemStack
    {
        private int _accelPoints;
        private int _itemCount;
        private int _level1Items;
        private int _level2Items;
        private int _level3Items;
        private int _level0Items;

        private ItemStack(int itemCount, int accPoints)
        {
            _itemCount = itemCount;
            _accelPoints = accPoints;

            _level0Items = 0;
            _level1Items = 0;
            _level2Items = 0;
            _level3Items = 0;
            var tmpAccPoints = accPoints;
            var remainCount = _itemCount;
            var vals = new int[]
            {
                4,
                2,
                1,
                0
            };
            if (tmpAccPoints <= _itemCount)
            {
                _level1Items = tmpAccPoints;
                _level0Items = itemCount - _level1Items;
                return;
            }

            if (tmpAccPoints <= _itemCount * 2)
            {
                _level2Items = tmpAccPoints / 2;
                _level1Items = itemCount - _level2Items;
                _level0Items = itemCount - _level1Items - _level2Items;
                return;
            }

            if (tmpAccPoints <= _itemCount * 4)
            {
                _level3Items = tmpAccPoints / 4;
                _level2Items = itemCount - _level3Items;
                _level1Items = itemCount - _level3Items - _level2Items;
                _level0Items = itemCount - _level3Items - _level2Items - _level1Items;
                return;
            }

            int curNdx = 0;
            while (remainCount > 0 && tmpAccPoints > 0 && curNdx < vals.Length)
            {
                var curMulti = vals[curNdx];
                if (curMulti * 1 > tmpAccPoints)
                {
                    curNdx++;
                    continue;
                }

                if (curMulti == 0)
                {
                    _level0Items = remainCount;
                    break;
                }

                switch (curNdx)
                {
                    case 0:
                        _level3Items++;
                        break;
                    case 1:
                        _level2Items++;
                        break;
                    case 2:
                        _level1Items++;
                        break;
                    case 3:
                        _level0Items++;
                        break;
                }

                tmpAccPoints -= curMulti;
                remainCount--;
            }
        }

        public static ItemStack FromCountAndPoints(int count, int proliferatorPoints)
        {
            return new ItemStack(count, proliferatorPoints);
        }

        public static ItemStack WithLevels(int count, int level1 = 0, int level2 = 0, int level3 = 0)
        {
            return new ItemStack(count, level1 + level2 * 2 + level3 * 4);
        }

        public int ItemCount => _itemCount;
        public int ProliferatorPoints => _itemCount == 0 ? 0 : _accelPoints;

        /// <summary>
        /// does 2 things, removes certain number of items and points from stack and returns new stack with amount removed and points removed  
        /// </summary>
        /// <param name="count">number of items to remove</param>
        /// <returns>new stack with amount removed and points removed</returns>
        public ItemStack Remove(int count)
        {
            var amountToActuallyRemove = Math.Min(_itemCount, count);
            var removeStack = new ItemStack(amountToActuallyRemove, ItemUtil
                .CalculateRatiodAmount(amountToActuallyRemove, _itemCount, _accelPoints));
            Subtract(removeStack);
            return removeStack;
        }

        public void Subtract(ItemStack other)
        {
            _itemCount -= other._itemCount;
            _accelPoints -= other._accelPoints;
            _level1Items -= other._level1Items;
            _level2Items -= other._level2Items;
            _level3Items -= other._level3Items;
        }

        public void Add(int count, int points)
        {
            Add(FromCountAndPoints(count, points));
        }

        public ItemStack Add(ItemStack newStack)
        {
            _itemCount += newStack._itemCount;
            _accelPoints += newStack._accelPoints;
            _level1Items += newStack._level1Items;
            _level2Items += newStack._level2Items;
            _level3Items += newStack._level3Items;
            return this;
        }

        public override string ToString()
        {
            var pointsPerItem = ItemCount == 0 ? 0 : _accelPoints / ItemCount;
            var sb = new StringBuilder();
            if (pointsPerItem > 0 && pointsPerItem < 2)
            {
                sb.Append($"{ItemCount} items at level 1");
            }

            return $"{ItemCount} {_accelPoints}  {sb}";
        }

        public ProliferatorPointSummary BuildSummary()
        {
            return new ProliferatorPointSummary
            {
                Level0Count = _level0Items,
                Level1Count = _level1Items,
                Level2Count = _level2Items,
                Level3Count = _level3Items,
            };
        }

        public int ItemsAtLevel1()
        {
            return _level1Items;
        }

        public int ItemsAtLevel2()
        {
            return _level2Items;
        }


        public int ItemsAtLevel3()
        {
            return _level3Items;
        }

        public static ItemStack Empty()
        {
            return new ItemStack(0, 0);
        }

        public bool IsFull()
        {
            return _itemCount == _level3Items;
        }
    }

    public class ProliferatorPointSummary : IEquatable<ProliferatorPointSummary>
    {
        public int Level0Count;
        public int Level1Count;
        public int Level2Count;
        public int Level3Count;

        public override string ToString()
        {
            return $@"Level 0: {Level0Count}
Level 1: {Level1Count}
Level 2: {Level2Count}
Level 3: {Level3Count}
";
        }


        public bool Equals(ProliferatorPointSummary other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return Level0Count == other.Level0Count
                   && Level1Count == other.Level1Count
                   && Level2Count == other.Level2Count
                   && Level3Count == other.Level3Count;
        }

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

            if (obj.GetType() != GetType())
            {
                return false;
            }

            return Equals((ProliferatorPointSummary)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = Level1Count;
                hashCode = (hashCode * 397) ^ Level2Count;
                hashCode = (hashCode * 397) ^ Level3Count;
                hashCode = (hashCode * 397) ^ Level0Count;
                return hashCode;
            }
        }
    }
}