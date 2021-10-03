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
        public long energyCost;
        public int planetId;
        public int stationId;
        public bool needWarper;
        public bool paid;
    }

    [Serializable]
    public class InventoryItem
    {
        public int itemId;
        public string itemName;
        public int count;
        public DateTime lastUpdated;

        public static InventoryItem Import(BinaryReader r)
        {
            var result = new InventoryItem
            {
                itemId = r.ReadInt32(),
                count = r.ReadInt32(),
                lastUpdated = DateTime.FromBinary(r.ReadInt64())
            };
            result.itemName = ItemUtil.GetItemName(result.itemId);

            return result;
        }

        public void Export(BinaryWriter binaryWriter)
        {
            binaryWriter.Write(itemId);
            binaryWriter.Write(count);
            binaryWriter.Write(lastUpdated.ToBinary());
        }
    }

    public class ItemBuffer
    {
        public int version = 1;
        public int seed;
        public List<InventoryItem> inventoryItems = new List<InventoryItem>();
        public Dictionary<int, InventoryItem> inventoryItemLookup = new Dictionary<int, InventoryItem>();

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
            int length = r.ReadInt32();
            Log.Debug($"Import length = {length}");
            for (int i = 0; i < length; i++)
            {
                var inventoryItem = InventoryItem.Import(r);
                result.inventoryItems.Add(inventoryItem);
                result.inventoryItemLookup[inventoryItem.itemId] = inventoryItem;
            }

            return result;
        }

        public void Export(BinaryWriter w)
        {
            w.Write(version);
            w.Write(seed);
            w.Write(inventoryItems.Count);
            Log.Debug($"Export length = {inventoryItems.Count}");

            foreach (var inventoryItem in inventoryItems)
            {
                inventoryItem.Export(w);
            }
        }

        public override string ToString()
        {
            return $"version={version}, seed={seed}, invItems={inventoryItems.Count}";
        }
    }

    public static class ShippingStatePersistence
    {
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
                    version = 1,
                    inventoryItems = new List<InventoryItem>(),
                    inventoryItemLookup = new Dictionary<int, InventoryItem>()
                };
                SaveState(state);
                return state;
            }

            try
            {
                using (FileStream fileStream = new FileStream(path, FileMode.Open, FileAccess.Read))
                {
                    using (BinaryReader r = new BinaryReader(fileStream))
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


        public static string saveFolder
        {
            get
            {
                if (savePath == null)
                {
                    savePath = new StringBuilder(GameConfig.overrideDocumentFolder).Append(GameConfig.gameName).Append("/PersonalLogistics/").ToString();
                    if (!Directory.Exists(savePath))
                        Directory.CreateDirectory(savePath);
                }

                return savePath;
            }
        }

        private static string savePath = null;


        private static string GetPath(int seed)
        {
            return Path.Combine(saveFolder, $"PersonalLogistics.{seed}.save");
        }

        public static void SaveState(ItemBuffer itemBuffer)
        {
            try
            {
                using (FileStream fileStream = new FileStream(GetPath(itemBuffer.seed), FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    using (BinaryWriter w = new BinaryWriter(fileStream))
                    {
                        itemBuffer.Export(w);
                        Log.Debug($"Saved item buffer {itemBuffer}");
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