using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using PersonalLogistics.Shipping;
using PersonalLogistics.Util;

namespace PersonalLogistics.Model
{
    public class ItemBuffer
    {
        private static readonly int VERSION = 3;
        private ConcurrentDictionary<int, InventoryItem> inventoryItemLookup = new();
        public int seed;
        public int version = VERSION;

        public int Count => inventoryItemLookup.Count;

        public void Remove(InventoryItem inventoryItem)
        {
            if (!inventoryItemLookup.TryRemove(inventoryItem.itemId, out _))
            {
                Log.Warn($"failed to remove item id {inventoryItem.itemId} from buffer");
            }
        }

        public bool HasItem(int itemId)
        {
            return inventoryItemLookup.ContainsKey(itemId);
        }

        public List<InventoryItem> GetInventoryItemView()
        {
            return new List<InventoryItem>(inventoryItemLookup.Values);
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
                InventoryItem inventoryItem;
                if (result.version > 2)
                {
                    // all items will have inc count values also
                    inventoryItem = InventoryItem.ImportV2(r);
                }
                else
                {
                    inventoryItem = InventoryItem.Import(r);
                }
                if (result.inventoryItemLookup.ContainsKey(inventoryItem.itemId))
                {
                    Log.Warn($"Multiple inv items for {inventoryItem.itemName} found, combining");
                    result.inventoryItemLookup[inventoryItem.itemId].count += inventoryItem.count;
                    itemsToDelete.Add(inventoryItem);
                }
                else
                {
                    result.inventoryItemLookup[inventoryItem.itemId] = inventoryItem;
                }

                if (result.version == 1)
                {
                    // migrate lastUpdated
                    inventoryItem.LastUpdated = GameMain.gameTick;
                }
            }

            if (result.version < VERSION)
            {
                result.version = VERSION;
                Log.Debug($"migrated version {result.version} save to version {VERSION}");
            }

            foreach (var itemToDelete in itemsToDelete)
            {
                result.inventoryItemLookup.TryRemove(itemToDelete.itemId, out var _);
            }

            return result;
        }

        public void Export(BinaryWriter w)
        {
            w.Write(version);
            w.Write(seed);
            w.Write(inventoryItemLookup.Count);

            foreach (var inventoryItem in inventoryItemLookup.Values)
            {
                inventoryItem.Export(w, version);
            }
        }

        public override string ToString() => $"version={version}, seed={seed}, invItems={inventoryItemLookup.Count}";

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
        }

        /// <returns>remaining count in buffer</returns>
        public int RemoveItemCount(int itemId, int amountToRemove)
        {
            if (!inventoryItemLookup.TryGetValue(itemId, out var inventoryItem))
            {
                Log.Warn($"tried to remove {itemId} from buffer but there was none in lookup");
                return 0;
            }
            
            if (amountToRemove >= inventoryItem.count)
            {
                Remove(inventoryItem);
                return 0;
            }
            inventoryItem.count -= amountToRemove;
            return inventoryItem.count;

        }
    }
}