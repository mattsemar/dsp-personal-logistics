using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
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
        public Dictionary<int, InventoryItem> inventoryItemLookup = new Dictionary<int, InventoryItem>();
        public List<InventoryItem> inventoryItems = new List<InventoryItem>();
        public int seed;
        public int version = 2;

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
    }

    public static class ShippingStatePersistence
    {
        private static string savePath;


        public static string SaveFolder
        {
            get
            {
                if (savePath == null)
                {
                    savePath = new StringBuilder(GameConfig.overrideDocumentFolder).Append(GameConfig.gameName).Append("/PersonalLogistics/").ToString();
                    if (!Directory.Exists(savePath))
                    {
                        Directory.CreateDirectory(savePath);
                    }
                }

                return savePath;
            }
        }

        public static bool LegacyExternalSaveExists(int seed)
        {
            var path = GetPath(seed);
            return File.Exists(path);
        }
        
        public static ItemBuffer LoadState(int seed)
        {
            Log.Debug($"load state for seed {seed}");
            var path = GetPath(seed);
            if (!File.Exists(path))
            {
                Log.Info($"PersonalLogistics.{seed}.save not found, path: {path}");
                var state = new ItemBuffer
                {
                    seed = seed,
                    inventoryItems = new List<InventoryItem>(),
                    inventoryItemLookup = new Dictionary<int, InventoryItem>()
                };
                SaveState(state);
                return state;
            }

            try
            {
                using (var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read))
                {
                    using (var r = new BinaryReader(fileStream))
                    {
                        return ItemBuffer.Import(r);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn($"Failed to load saved shipping data {e.Message}  {e.StackTrace}");
            }

            return new ItemBuffer
            {
                seed = seed
            };
        }


        private static string GetPath(int seed) => Path.Combine(SaveFolder, $"PersonalLogistics.{seed}.save");

        public static void SaveState(ItemBuffer itemBuffer)
        {
            try
            {
                if (!DSPGame.IsMenuDemo)
                {
                    Log.Debug("SaveState still being used, perhaps this is the first load after new version?");
                }
                else
                {
                    return;
                }

                using (var fileStream = new FileStream(GetPath(itemBuffer.seed), FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    using (var w = new BinaryWriter(fileStream))
                    {
                        itemBuffer.Export(w);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warn($"Failed to save state for seed {itemBuffer.seed} {ex.Message} {ex.StackTrace}");
            }
        }
    }
}