using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization.Formatters.Binary;
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
    }

    [Serializable]
    public class ItemBuffer
    {
        public int seed;
        public int version = 1;
        public List<InventoryItem> inventoryItems = new List<InventoryItem>();
        [NonSerialized] public Dictionary<int, InventoryItem> inventoryItemLookup = new Dictionary<int, InventoryItem>();

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
    }

    public static class ShippingStatePersistence
    {
        // public static string PluginPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

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
            else
            {
                FileStream fileStream = new FileStream(path, FileMode.Open);
                var formatter = new BinaryFormatter();
                var state = (ItemBuffer)formatter.Deserialize(fileStream);
                fileStream.Close();
                fileStream.Dispose();

                if (state.seed == 0)
                    throw new Exception("Invalid seed found while loading");
                foreach (var item in state.inventoryItems)
                {
                    state.inventoryItemLookup[item.itemId] = item;
                }

                return state;
            }
        }

        private static string GetPath(int seed)
        {
            var filePath = FileUtil.GetPluginFolderName();
            return Path.Combine(filePath, $"PersonalLogistics.{seed}.save");
        }

        public static void SaveState(ItemBuffer itemBuffer)
        {
            if (itemBuffer.seed == 0)
                throw new Exception("Invalid seed found while saving");
            Stream stream = File.Open(GetPath(itemBuffer.seed), FileMode.OpenOrCreate);
            BinaryFormatter formatter = new BinaryFormatter();
            formatter.Serialize(stream, itemBuffer);
            stream.Close();
            stream.Dispose();
        }
    }
}