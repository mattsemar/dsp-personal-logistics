using System;
using System.IO;
using PersonalLogistics.Util;

namespace PersonalLogistics.Model
{
    [Serializable]
    public class InventoryItem
    {
        public readonly int itemId;
        public string itemName;
        public int count;
        public int proliferatorPoints;
        private readonly int _maxStackSize;
        private long _lastUpdated;

        public InventoryItem(int itemId)
        {
            this.itemId = itemId;
            _maxStackSize = ItemUtil.GetItemProto(itemId).StackSize;
            itemName = ItemUtil.GetItemName(itemId);
            _lastUpdated = GameMain.gameTick;
        }

        public long AgeInSeconds => TimeUtil.GetSecondsFromGameTicks(GameMain.gameTick - _lastUpdated);

        public long LastUpdated
        {
            get => _lastUpdated;
            set => _lastUpdated = value;
        }

        public static InventoryItem Import(BinaryReader r)
        {
            var result = new InventoryItem(r.ReadInt32())
            {
                count = r.ReadInt32(),
                _lastUpdated = r.ReadInt64()
            };
            return result;
        }

        public void Export(BinaryWriter binaryWriter, int version = 2)
        {
            binaryWriter.Write(itemId);
            binaryWriter.Write(count);
            binaryWriter.Write(_lastUpdated);
            if (version > 2)
            {
                binaryWriter.Write(proliferatorPoints);
            }
        }

        public int GetMaxStackSize() => _maxStackSize;

        public static InventoryItem ImportV2(BinaryReader r)
        {
            // only difference is accelerant added to items (inc)
            var inventoryItem = Import(r);
            inventoryItem.proliferatorPoints = r.ReadInt32();
            return inventoryItem;
        }

        public ItemStack ToItemStack()
        {
            return ItemStack.FromCountAndPoints(count, proliferatorPoints);
        }

        public InventoryItem Clone()
        {
            return new InventoryItem(itemId)
            {
                count = count,
                itemName = itemName,
                _lastUpdated = _lastUpdated,
                proliferatorPoints = proliferatorPoints,
            };
        }
    }
}