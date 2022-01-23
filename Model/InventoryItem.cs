using System;
using System.IO;
using PersonalLogistics.Util;

namespace PersonalLogistics.Model
{
    [Serializable]
    public class InventoryItem
    {
        public int itemId;
        public string itemName;
        public int count;
        public int accelerationFactor;
        private readonly int _maxStackSize;
        private long _lastUpdated;

        public InventoryItem(int itemId)
        {
            this.itemId = itemId;
            _maxStackSize = ItemUtil.GetItemProto(itemId).StackSize;
            itemName = ItemUtil.GetItemName(itemId);
        }

        public long AgeInSeconds => (GameMain.gameTick - _lastUpdated) / 60;

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
                binaryWriter.Write(accelerationFactor);
            }
        }

        public int GetMaxStackSize() => _maxStackSize;

        public static InventoryItem ImportV2(BinaryReader r)
        {
            // only difference is accelerant added to items (inc)
            var inventoryItem = Import(r);
            inventoryItem.accelerationFactor = r.ReadInt32();
            return inventoryItem;
        }
    }
}