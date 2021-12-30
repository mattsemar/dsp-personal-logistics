using System;
using System.Collections.Generic;
using System.IO;
using PersonalLogistics.Util;
using UnityEngine;

namespace PersonalLogistics.Model
{
    [Serializable]
    public class DesiredItem
    {
        private static readonly int VERSION = 1;
        public readonly int itemId;
        public int count;
        public int maxCount;
        public bool allowBuffering = true;
        public static DesiredItem bannedDesiredItem = new DesiredItem(0) { count = 0, maxCount = 0 };
        public static DesiredItem notRequestedDesiredItem = new DesiredItem(0) { count = 0, maxCount = Int32.MaxValue };
        private int stackSize;

        public DesiredItem(int newItemId)
        {
            itemId = newItemId;
            var itemProto = ItemUtil.GetItemProto(newItemId);
            if (itemProto != null)
                stackSize = itemProto.StackSize;
        }

        public bool IsBanned()
        {
            return maxCount == 0;
        }

        public bool IsNonRequested()
        {
            return count < 1;
        }

        public int RequestedStacks()
        {
            return stackSize > 0 ? Mathf.CeilToInt(count / (float)stackSize) : count;
        }

        public int RecycleMaxStacks()
        {
            if (!IsRecycle())
                return 300;
            return stackSize > 0 ? Mathf.CeilToInt(maxCount / (float)stackSize) : maxCount;
        }

        public bool IsRecycle()
        {
            if (maxCount < 0)
                return false;
            return stackSize > 0 ? maxCount / stackSize < 300 : maxCount < 1_000_000;
        }

        public bool IsNonManaged()
        {
            return IsNonRequested() && !IsRecycle();
        }

        public void Export(BinaryWriter binaryWriter)
        {
            binaryWriter.Write(VERSION);
            binaryWriter.Write(itemId);
            binaryWriter.Write(count);
            binaryWriter.Write(maxCount);
            binaryWriter.Write(allowBuffering);
        }

        public static DesiredItem Import(BinaryReader r)
        {
            var ver = r.ReadInt32();
            if (ver != VERSION)
            {
                Log.Debug($"reading an older version of desired item: {ver}, {VERSION}");
            }

            return new DesiredItem(r.ReadInt32())
            {
                count = r.ReadInt32(),
                maxCount = r.ReadInt32(),
                allowBuffering = r.ReadBoolean()
            };
        }

        public override string ToString() => $"DesiredItem: count={count}, max={maxCount}, stackSize={stackSize}";
    }

    public enum DesiredInventoryAction
    {
        None, // no action needed
        Add, // add more of this item
        Remove // remove item
    }

    public class DesiredInventoryState
    {
        private static readonly int VERSION = 1;
        public readonly HashSet<int> BannedItems = new HashSet<int>();
        public readonly Dictionary<int, DesiredItem> DesiredItems = new Dictionary<int, DesiredItem>();
        private readonly string _seed;
        private static DesiredInventoryState _instance;
        public static DesiredInventoryState instance => GetInstance();

        private DesiredInventoryState() : this(GameUtil.GetSeed())
        {
            // private, access through get instance
        }

        private DesiredInventoryState(string seed)
        {
            _seed = seed;
        }

        private static DesiredInventoryState GetInstance()
        {
            if (_instance != null)
            {
                if (_instance._seed != GameUtil.GetSeed())
                {
                    Log.Debug($"Re-initting desired inventory state on seed change {_instance._seed} != {GameUtil.GetSeedInt()}");
                }
                else
                {
                    return _instance;
                }
            }

            _instance = new DesiredInventoryState();
            _instance.TryLoadFromConfig();
            return _instance;
        }

        private void TryLoadFromConfig()
        {
            var strVal = PluginConfig.crossSeedInvState.Value;
            // format is "seedStr__JSONREP$seedStr__JSONREP"
            var parts = strVal.Split('$');
            // var states = new List<DesiredInventoryState>();

            if (parts.Length < 1)
            {
                Log.Debug($"Desired items found no state in config for seed");
                return;
            }

            foreach (var savedValueForSeedStr in parts)
            {
                var firstUnderscoreIndex = savedValueForSeedStr.IndexOf('_');
                if (firstUnderscoreIndex == -1)
                {
                    Log.Warn($"failed to convert parts into seed and JSON {savedValueForSeedStr}");
                    continue;
                }

                var seedString = savedValueForSeedStr.Substring(0, firstUnderscoreIndex);
                var jsonStrWithLeadingUnderscore = savedValueForSeedStr.Substring(firstUnderscoreIndex + 1);
                if (seedString.Length < 3 || jsonStrWithLeadingUnderscore[0] != '_')
                {
                    Log.Warn($"invalid parsing of parts {seedString} {jsonStrWithLeadingUnderscore}");
                    continue;
                }

                if (seedString != _seed)
                {
                    Log.Debug($"skipping seed {seedString} from config while loading DesiredInvState");
                    continue;
                }

                try
                {
                    InvStateSerializable serState = JsonUtility.FromJson<InvStateSerializable>(jsonStrWithLeadingUnderscore.Substring(1));

                    for (var i = 0; i < serState.itemIds.Count; i++)
                    {
                        var item = new DesiredItem(serState.itemIds[i]) { count = serState.counts[i], maxCount = serState.maxCounts[i] };
                        if (item.maxCount == 0)
                        {
                            AddBan(item.itemId);
                        }
                        else
                        {
                            if (item.count < 0)
                            {
                                AddDesiredItem(item.itemId, -item.count, item.maxCount, false);
                            }
                            else
                            {
                                AddDesiredItem(item.itemId, item.count, item.maxCount);
                            }
                        }
                    }

                    Log.Debug($"Loaded desired inv state from config property");
                    break;
                }
                catch (Exception e)
                {
                    Log.Warn($"Failed to deserialize stored inventory state {e}\r\n{e.StackTrace}");
                }
            }
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

            if (DesiredItems.TryGetValue(itemId, out var item))
            {
                if (item.count == count)
                {
                    return (DesiredInventoryAction.None, 0, false);
                }

                if (item.count <= item.maxCount && item.maxCount < count)
                {
                    // delete excess
                    return (DesiredInventoryAction.Remove, count - item.maxCount, false);
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

        public static void Export(BinaryWriter w)
        {
            if (_instance == null)
            {
                Log.Debug("no export of DesiredInventoryState since instance is null");
                return;
            }

            w.Write(VERSION);
            w.Write(_instance._seed);
            w.Write(_instance.BannedItems.Count);
            foreach (var bannedItem in _instance.BannedItems)
            {
                w.Write(bannedItem);
            }

            w.Write(_instance.DesiredItems.Count);
            foreach (var desiredItem in _instance.DesiredItems.Values)
            {
                desiredItem.Export(w);
            }
        }

        public static void Import(BinaryReader r)
        {
            var ver = r.ReadInt32();
            if (ver != VERSION)
            {
                Log.Debug($"desired inventory state version from save: {ver} does not match mod version: {VERSION}");
            }

            _instance = new DesiredInventoryState(r.ReadString());
            var bannedCount = r.ReadInt32();
            for (var i = 0; i < bannedCount; i++)
            {
                _instance.BannedItems.Add(r.ReadInt32());
            }

            var desiredCount = r.ReadInt32();
            for (var i = 0; i < desiredCount; i++)
            {
                DesiredItem di = DesiredItem.Import(r);
                _instance.DesiredItems[di.itemId] = di;
            }

            Log.Debug($"Imported version: {VERSION} desired inventory state from save file. Found {bannedCount} banned items and {desiredCount} desired items");
        }

        public static void InitOnLoad()
        {
            var desiredInventoryState = GetInstance();
            Log.Debug($"Initted desired inv state, bannedCount={desiredInventoryState.BannedItems.Count}, desiredCount={desiredInventoryState.DesiredItems.Count}");
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