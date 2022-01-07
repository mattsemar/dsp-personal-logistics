using System;
using System.Collections.Generic;
using System.IO;
using PersonalLogistics.Util;

namespace PersonalLogistics.Shipping
{
    [Serializable]
    public class Cost
    {
        private static readonly int VERSION = 1;
        public long energyCost;
        public int planetId;
        public int stationId;
        public bool needWarper;
        public bool paid;
        public long paidTick;

        public static Cost Import(BinaryReader r)
        {
            var version = r.ReadInt32();
            if (version != VERSION)
            {
                Log.Warn($"version mismatch on cost {VERSION} vs {version}");
            }

            var result = new Cost
            {
                energyCost = r.ReadInt64(),
                planetId = r.ReadInt32(),
                stationId = r.ReadInt32(),
                needWarper = r.ReadBoolean(),
                paid = r.ReadBoolean(),
                paidTick = r.ReadInt64()
            };
            return result;
        }

        public void Export(BinaryWriter binaryWriter)
        {
            binaryWriter.Write(VERSION);
            binaryWriter.Write(energyCost);
            binaryWriter.Write(planetId);
            binaryWriter.Write(stationId);
            binaryWriter.Write(needWarper);
            binaryWriter.Write(paid);
            binaryWriter.Write(paidTick);
        }
    }

    [Serializable]
    public class InventoryItem
    {
        public int itemId;
        public string itemName;
        public int count;
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

        public void Export(BinaryWriter binaryWriter)
        {
            binaryWriter.Write(itemId);
            binaryWriter.Write(count);
            binaryWriter.Write(_lastUpdated);
        }

        public int GetMaxStackSize() => _maxStackSize;
    }

    public class ItemBuffer
    {
        private Dictionary<int, InventoryItem> inventoryItemLookup = new();
        private List<InventoryItem> inventoryItems = new();
        public int seed;
        public int version = 2;

        public int Count => inventoryItems.Count;

        public void Remove(InventoryItem inventoryItem)
        {
            if (!inventoryItems.Remove(inventoryItem))
            {
                Log.Warn($"Failed to actually remove inventoryItem {inventoryItem} from invItems");
            }

            if (!inventoryItemLookup.Remove(inventoryItem.itemId))
            {
                Log.Warn($"Lookup key not found for item id {inventoryItem.itemId}");
            }
        }

        public bool HasItem(int itemId)
        {
            return inventoryItemLookup.ContainsKey(itemId);
        }

        public List<InventoryItem> GetInventoryItemView()
        {
            return new List<InventoryItem>(inventoryItems);
        }

        public static ItemBuffer Import(BinaryReader r)
        {
            var result = new ItemBuffer
            {
                version = r.ReadInt32(),
                seed = r.ReadInt32()
            };
            var length = r.ReadInt32();
            Log.Debug($"Import length = {length}");

            var itemsToDelete = new List<InventoryItem>();

            for (var i = 0; i < length; i++)
            {
                var inventoryItem = InventoryItem.Import(r);
                if (result.inventoryItemLookup.ContainsKey(inventoryItem.itemId))
                {
                    Log.Warn($"Multiple inv items for {inventoryItem.itemName} found, combining");
                    result.inventoryItemLookup[inventoryItem.itemId].count += inventoryItem.count;
                    itemsToDelete.Add(inventoryItem);
                }
                else
                {
                    result.inventoryItems.Add(inventoryItem);
                    result.inventoryItemLookup[inventoryItem.itemId] = inventoryItem;
                }

                if (result.version == 1)
                {
                    // migrate lastUpdated
                    inventoryItem.LastUpdated = GameMain.gameTick;
                }
            }

            if (result.version < 2)
            {
                result.version = 2;
                Log.Debug($"migrated version {result.version} save to version 2");
            }

            foreach (var itemToDelete in itemsToDelete)
            {
                result.inventoryItems.Remove(itemToDelete);
            }

            return result;
        }

        public void Export(BinaryWriter w)
        {
            w.Write(version);
            w.Write(seed);
            w.Write(inventoryItems.Count);

            foreach (var inventoryItem in inventoryItems)
            {
                inventoryItem.Export(w);
            }
        }

        public override string ToString() => $"version={version}, seed={seed}, invItems={inventoryItems.Count}";

        public int GetItemCount(int itemId)
        {
            return inventoryItemLookup.ContainsKey(itemId) ? inventoryItemLookup[itemId].count : 0;
        }

        public InventoryItem GetItem(int itemId)
        {
            if (inventoryItemLookup.ContainsKey(itemId))
                return inventoryItemLookup[itemId];
            return null;
        }

        public void AddItem(InventoryItem invItem)
        {
            inventoryItemLookup[invItem.itemId] = invItem;
            inventoryItems.Add(invItem);
        }
    }
}