using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PersonalLogistics.Nebula;
using PersonalLogistics.Nebula.Client;
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

        public bool HasItem(int itemId)
        {
            return inventoryItemLookup.ContainsKey(itemId);
        }

        public List<InventoryItem> GetInventoryItemView()
        {
            return new List<InventoryItem>(inventoryItemLookup.Values.Select(invI => invI.Clone()));
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

        /// <returns>stack removed from buffer</returns>
        public ItemStack RemoveItemCount(int itemId, int amountToRemove)
        {
            if (!inventoryItemLookup.TryGetValue(itemId, out var inventoryItem))
            {
                Log.Warn($"tried to remove {itemId} from buffer but there was none in lookup");
                // since the caller was out of sync on client, host probably is too, sync it up
                SendUpsertPacket(itemId);
                return ItemStack.Empty();
            }

            if (amountToRemove >= inventoryItem.count)
            {
                var removedAmount = inventoryItem.ToItemStack();
                inventoryItemLookup.TryRemove(inventoryItem.itemId, out _);
                // if (NebulaLoadState.IsMultiplayerClient())
                //     RequestClient.NotifyBufferUpsert(itemId, ItemStack.Empty(), GameMain.gameTick);
                SendUpsertPacket(itemId);
                return removedAmount;
            }

            // inventoryItem.count -= amountToRemove;
            var result = inventoryItem.ToItemStack().Remove(amountToRemove);
            SendUpsertPacket(itemId);
            return result;
        }

        public void UpsertItem(int itemId, ItemStack stack, long gameTickUpdated = 0)
        {
            if (!inventoryItemLookup.TryGetValue(itemId, out var invItem))
            {
                invItem = new InventoryItem(itemId)
                {
                    count = stack.ItemCount,
                    proliferatorPoints = stack.ProliferatorPoints,
                };
                inventoryItemLookup[itemId] = invItem;
            }
            else
            {
                invItem.count = stack.ItemCount;
                invItem.proliferatorPoints = stack.ProliferatorPoints;
            }

            invItem.LastUpdated = gameTickUpdated > 0 ? gameTickUpdated : GameMain.gameTick;

            if (invItem.count <= 0)
            {
                inventoryItemLookup.TryRemove(itemId, out _);
            }

            SendUpsertPacket(itemId);
        }

        private void SendUpsertPacket(int itemId)
        {
            if (!NebulaLoadState.IsMultiplayerClient())
            {
                return;
            }

            if (!inventoryItemLookup.TryGetValue(itemId, out var invItem))
                RequestClient.NotifyBufferUpsert(itemId, ItemStack.Empty(), GameMain.gameTick);
            else RequestClient.NotifyBufferUpsert(itemId, invItem.ToItemStack(), GameMain.gameTick);
        }

        public bool Add(int itemId, ItemStack stack)
        {
            if (!inventoryItemLookup.TryGetValue(itemId, out var inventoryItem))
            {
                inventoryItem = new InventoryItem(itemId)
                {
                    // count = stack.ItemCount,
                    // proliferatorPoints = stack.ProliferatorPoints,
                    itemName = ItemUtil.GetItemName(itemId),
                };
                inventoryItemLookup[itemId] = inventoryItem;
            }

            if (inventoryItem.count + stack.ItemCount > 100_000)
            {
                Log.Warn($"No more storage available for item {ItemUtil.GetItemName(itemId)}, count {inventoryItem.count}");
                return false;
            }

            var result = inventoryItem.ToItemStack().Add(stack);
            inventoryItem.count = result.ItemCount;
            inventoryItem.proliferatorPoints = result.ProliferatorPoints;
            inventoryItem.LastUpdated = GameMain.gameTick;
            // inventoryItem.count += stack.ItemCount;
            // inventoryItem.proliferatorPoints += stack.ProliferatorPoints;
            SendUpsertPacket(itemId);
            return true;
        }

#if DEBUG
        public void Clear()
        {
            inventoryItemLookup.Clear();
        }
#endif
    }
}