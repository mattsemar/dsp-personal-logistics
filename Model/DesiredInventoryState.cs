using System;
using System.Collections.Generic;
using PersonalLogistics.Util;
using UnityEngine;

namespace PersonalLogistics.Model
{
    [Serializable]
    public class DesiredItem
    {
        public int count;
        public int itemId;
        public int maxCount;
        public bool allowBuffering = true;
    }

    public enum DesiredInventoryAction
    {
        None, // no action needed
        Add, // add more of this item
        Remove, // remove item
    }

    public class DesiredInventoryState
    {
        public HashSet<int> BannedItems = new HashSet<int>();
        public Dictionary<int, DesiredItem> DesiredItems = new Dictionary<int, DesiredItem>();

        private bool IsBanned(int itemId)
        {
            return BannedItems.Contains(itemId);
        }

        public void AddBan(int itemId)
        {
            if (DesiredItems.ContainsKey(itemId))
            {
                Log.Debug($"removing item from desired list, only in ban now {itemId}");
                DesiredItems.Remove(itemId);
            }

            BannedItems.Add(itemId);
            CrossSeedInventoryState.instance?.SaveState();
        }

        public (DesiredInventoryAction action, int actionCount, bool skipBuffer) GetActionForItem(int itemId, int count)
        {
            if (IsBanned(itemId))
            {
                if (count > 0)
                {
                    return (DesiredInventoryAction.Remove, count, false);
                }

                return (DesiredInventoryAction.None, 0, false);
            }

            if (DesiredItems.TryGetValue(itemId, out DesiredItem item))
            {
                if (item.count == count)
                {
                    return (DesiredInventoryAction.None, 0, false);
                }

                if (item.count <= item.maxCount && item.maxCount < count)
                {
                    // delete excess
                    return (DesiredInventoryAction.Remove, count - item.maxCount,  false);
                }

                if (item.count > count)
                {
                    // need more, please
                    return (DesiredInventoryAction.Add, item.count - count, !item.allowBuffering);
                }
            }

            return (DesiredInventoryAction.None, 0, false);
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

        public Boolean IsDesiredOrBanned(int itemId)
        {
            return BannedItems.Contains(itemId) || DesiredItems.ContainsKey(itemId);
        }

        public void AddDesiredItem(int itemId, int itemCount, int maxCount = -1, bool allowBuffering = true)
        {
            if (IsBanned(itemId) && itemCount > 0)
            {
                throw new Exception($"Banned item can't be desired {itemId} {itemCount}");
            }

            if (!DesiredItems.ContainsKey(itemId))
            {
                DesiredItems.Add(itemId, new DesiredItem { count = Math.Abs( itemCount), itemId = itemId, maxCount = maxCount, allowBuffering = allowBuffering});
            }
            else
            {
                DesiredItems[itemId].count = itemCount; 
                DesiredItems[itemId].maxCount = maxCount; 
            }
            CrossSeedInventoryState.instance?.SaveState();
        }

        public static DesiredInventoryState LoadStored(string storedStateString)
        {
            return InvStateSerializable.FromSerializable(JsonUtility.FromJson<InvStateSerializable>(storedStateString));
        }

        public string SerializeToString()
        {
            try
            {
                var serState = InvStateSerializable.FromDesiredInventoryState(this);
                return JsonUtility.ToJson(serState);
            }
            catch (Exception e)
            {
                Log.Warn($"failed to convert state to serializable string {e} {e.StackTrace}");
            }

            return "{}";
        }

        public void ClearAll()
        {
            BannedItems.Clear();
            DesiredItems.Clear();
        }
    }

    [Serializable]
    public class InvStateSerializable
    {
        [SerializeField] private List<int> itemIds;
        [SerializeField] private List<int> counts;
        [SerializeField] private List<int> maxCounts;

        public InvStateSerializable(List<DesiredItem> desiredItems)
        {
            itemIds = new List<int>();
            counts = new List<int>();
            maxCounts = new List<int>();
            for (int i = 0; i < desiredItems.Count; i++)
            {
                var desiredItem = desiredItems[i];
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
                var desiredItem = new DesiredItem { count = 0, itemId = stateBannedItem, maxCount = 0 };
                items.Add(desiredItem);
            }

            var result = new InvStateSerializable(items);
            return result;
        }

        public static DesiredInventoryState FromSerializable(InvStateSerializable serInp)
        {
            var result = new DesiredInventoryState();
            for (int i = 0; i < serInp.itemIds.Count; i++)
            {
                var item = new DesiredItem { itemId = serInp.itemIds[i], count = serInp.counts[i], maxCount = serInp.maxCounts[i] };
                if (item.maxCount == 0)
                {
                    result.AddBan(item.itemId);
                }
                else
                {
                    if (item.count < 0)
                    {
                        result.AddDesiredItem(item.itemId, -item.count, item.maxCount, allowBuffering: false);   
                    }
                    else
                    {
                        result.AddDesiredItem(item.itemId, item.count, item.maxCount);
                    }
                }
            }

            return result;
        }
    }
}