using System;
using System.Collections.Generic;
using System.IO;
using PersonalLogistics.Util;
using UnityEngine;

namespace PersonalLogistics.Model
{
    public enum DesiredInventoryAction
    {
        None, // no action needed
        Add, // add more of this item
        Remove // remove item
    }

    public class DesiredInventoryState
    {
        private static readonly int VERSION = 1;
        public readonly HashSet<int> BannedItems = new();
        public readonly Dictionary<int, DesiredItem> DesiredItems = new();
        private readonly string _seed;

        public DesiredInventoryState(string seed)
        {
            _seed = seed;
        }

        private bool IsBanned(int itemId) => BannedItems.Contains(itemId);

        public void AddBan(int itemId)
        {
            if (DesiredItems.ContainsKey(itemId))
            {
                Log.Debug($"removing item from desired list, only in ban now {itemId}");
                DesiredItems.Remove(itemId);
            }

            BannedItems.Add(itemId);
        }

        public (DesiredInventoryAction action, int actionCount, bool skipBuffer, int incAmount) GetActionForItem(int itemId, ItemStack stack)
        {
            if (IsBanned(itemId))
            {
                if (stack.ItemCount > 0)
                {
                    return (DesiredInventoryAction.Remove, stack.ItemCount, false, stack.ProliferatorPoints);
                }

                return (DesiredInventoryAction.None, 0, false, 0);
            }

            if (DesiredItems.TryGetValue(itemId, out var item))
            {
                if (item.count == stack.ItemCount)
                {
                    return (DesiredInventoryAction.None, 0, false, 0);
                }

                if (item.count <= item.maxCount && item.maxCount < stack.ItemCount)
                {
                    // delete excess
                    var removeAmount = ItemStack.FromCountAndPoints(stack.ItemCount, stack.ProliferatorPoints)
                        .Remove(stack.ItemCount - item.maxCount);
                    Log.Debug($"found {stack.ItemCount} of item in inv. removing {removeAmount.ItemCount} to get below {item.maxCount}. Also removing {removeAmount.ProliferatorPoints} out of {stack.ProliferatorPoints}");
                    return (DesiredInventoryAction.Remove, removeAmount.ItemCount, false, removeAmount.ProliferatorPoints);
                }

                if (item.count > stack.ItemCount)
                {
                    // need more, please
                    return (DesiredInventoryAction.Add, item.count - stack.ItemCount, !item.allowBuffering, 0);
                }
            }

            return (DesiredInventoryAction.None, 0, false, 0);
        }

        public List<ItemProto> GetAllDesiredItems()
        {
            var result = new List<ItemProto>();
            foreach (var itemProto in ItemUtil.GetAllItems())
            {
                if (DesiredItems.ContainsKey(itemProto.ID))
                {
                    result.Add(itemProto);
                }
            }

            return result;
        }

        public bool IsDesiredOrBanned(int itemId) => BannedItems.Contains(itemId) || DesiredItems.ContainsKey(itemId);

        public void AddDesiredItem(int itemId, int itemCount, int maxCount = -1, bool allowBuffering = true)
        {
            if (IsBanned(itemId) && itemCount > 0)
            {
                throw new Exception($"Banned item can't be desired {itemId} {itemCount}");
            }

            if (!DesiredItems.ContainsKey(itemId))
            {
                DesiredItems.Add(itemId, new DesiredItem(itemId) { count = Math.Abs(itemCount), maxCount = maxCount, allowBuffering = allowBuffering });
            }
            else
            {
                DesiredItems[itemId].count = itemCount;
                DesiredItems[itemId].maxCount = maxCount;
            }
        }

        public void ClearAll()
        {
            BannedItems.Clear();
            DesiredItems.Clear();
        }

        public void Export(BinaryWriter w)
        {
            Log.Debug($"Writing out desired inventory state. {BannedItems.Count} {DesiredItems.Count}");
            w.Write(VERSION);
            w.Write(_seed);
            w.Write(BannedItems.Count);
            foreach (var bannedItem in BannedItems)
            {
                w.Write(bannedItem);
            }

            w.Write(DesiredItems.Count);
            foreach (var desiredItem in DesiredItems.Values)
            {
                desiredItem.Export(w);
            }
        }

        public static DesiredInventoryState Import(BinaryReader r)
        {
            var ver = r.ReadInt32();
            if (ver != VERSION)
            {
                Log.Debug($"desired inventory state version from save: {ver} does not match mod version: {VERSION}");
            }

            var result = new DesiredInventoryState(r.ReadString());
            var bannedCount = r.ReadInt32();
            for (var i = 0; i < bannedCount; i++)
            {
                result.BannedItems.Add(r.ReadInt32());
            }

            var desiredCount = r.ReadInt32();
            result.DesiredItems.Clear();
            for (var i = 0; i < desiredCount; i++)
            {
                DesiredItem di = DesiredItem.Import(r);
                result.DesiredItems[di.itemId] = di;
            }

            Log.Debug($"Imported version: {VERSION} desired inventory state from save file. Found {bannedCount} banned items and {desiredCount} desired items");
            return result;
        }
    }

    [Serializable]
    public class InvStateSerializable
    {
        [SerializeField] public List<int> itemIds;
        [SerializeField] public List<int> counts;
        [SerializeField] public List<int> maxCounts;

        public InvStateSerializable(List<DesiredItem> desiredItems)
        {
            itemIds = new List<int>();
            counts = new List<int>();
            maxCounts = new List<int>();
            foreach (var desiredItem in desiredItems)
            {
                itemIds.Add(desiredItem.itemId);
                var desiredItemCount = desiredItem.count;
                if (!desiredItem.allowBuffering)
                {
                    // encode this buffering flag on the item count so the format does not have to change
                    desiredItemCount = -desiredItemCount;
                }

                counts.Add(desiredItemCount);
                maxCounts.Add(desiredItem.maxCount);
            }
        }

        public static InvStateSerializable FromDesiredInventoryState(DesiredInventoryState state)
        {
            var items = new List<DesiredItem>(state.DesiredItems.Values);
            foreach (var stateBannedItem in state.BannedItems)
            {
                var desiredItem = new DesiredItem(stateBannedItem) { count = 0, maxCount = 0 };
                items.Add(desiredItem);
            }

            var result = new InvStateSerializable(items);
            return result;
        }
    }
}