using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using BepInEx.Configuration;
using PersonalLogistics.Model;
using PersonalLogistics.Nebula;
using PersonalLogistics.SerDe;
using static PersonalLogistics.Util.Log;

namespace PersonalLogistics.Util
{
    public enum CheatLevel
    {
        [Description("Instantly generate desired items and add to inventory without affecting any logistics stations")]
        Full,

        [Description("Take items from any supplying stations but with no warpers or shipping energy cost")]
        Quarter,

        [Description("Take items from any station with no shipping cost (will use storage, supply or demand)")]
        Half,

        [Description(
            "Only take items from stations on local planet without shipping cost or delay")]
        Planetary
    }

    public class PluginConfig
    {
        public static ConfigEntry<CheatLevel> cheatLevel;
        public static ConfigEntry<bool> sortInventory;
        public static ConfigEntry<bool> inventoryManagementPaused;
        public static ConfigEntry<bool> playerConfirmedTrash;
        public static ConfigEntry<bool> sendLitterToLogisticsNetwork;
        public static ConfigEntry<int> minRecycleDelayInSeconds;
        public static ConfigEntry<int> proliferatorPointBoost;
        public static ConfigEntry<bool> neverUseMechaEnergy;
        public static ConfigEntry<bool> neverUseMechaWarper;
        public static ConfigEntry<int> minStacksToLoadFromStations;

        public static ConfigEntry<string> originalButtonPosition;
        public static ConfigEntry<string> originalButtonSz;

        public static ConfigEntry<bool> showRecycleWindow;
        public static ConfigEntry<bool> showAmountsInRequestWindow;
        public static ConfigEntry<bool> addFuelToMecha;
        public static Dictionary<int, ConfigEntry<bool>> enabledFuelItems = new();
        public static ConfigEntry<bool> addWarpersToMecha;

        public static ConfigEntry<int> testExportOverrideVersion;
        public static ConfigEntry<string> testOverrideLanguage;
        public static ConfigEntry<string> multiplayerUserId;


        public static ConfigFile configFile { get; private set; }

        public static void InitConfig(ConfigFile confFile)
        {
            if (configFile != null)
            {
                return;
            }

            cheatLevel = confFile.Bind("Logistics", "Plugin Cheat Level", CheatLevel.Full,
                "How cheaty do we want to be?");

            sortInventory = confFile.Bind("Inventory", "SortInventory", true,
                "Enable/disable sorting of inventory after items are added/removed");
            inventoryManagementPaused = confFile.Bind("Inventory", "InventoryManagementPaused", false,
                new ConfigDescription("Temporarily pause management of player inventory", null, "configEditOnly"));
            playerConfirmedTrash = confFile.Bind("Inventory", "Player Confirmed Trash Recycler", false,
                new ConfigDescription(
                    "Stores whether the player has confirmed that they are aware that littered items will be sent to logistics network",
                    null, "configEditOnly"));
            sendLitterToLogisticsNetwork = confFile.Bind("Inventory", "SendLitterToLogisticsNetwork", true,
                "Use personal logistics system to send littered items to nearby logistics stations");
            minStacksToLoadFromStations = confFile.Bind("Inventory", "Min Stacks To Load", 0,
                new ConfigDescription(
                    "Don't load items from stations if there are not at least this many stacks of the item available",
                    new AcceptableValueRange<int>(0, 10)));
            addFuelToMecha = confFile.Bind("Inventory", "Add fuel to mecha fuel chamber", false,
                "Add fuel from inventory to mecha, any usable fuel found will be used");
            addWarpersToMecha = confFile.Bind("Inventory", "Add warpers to mecha", false,
                "Add warpers from inventory to mecha, requires that warpers be available in Logistics Network and currently requested");
            minRecycleDelayInSeconds = confFile.Bind("Inventory", "Min Recycle Delay Seconds", 10,
                new ConfigDescription("Minimum wait time before items in recycle area are removed",
                    new AcceptableValueRange<int>(0, 100)));
            proliferatorPointBoost = confFile.Bind("Inventory", "Proliferator Point Boost", 3,
                new ConfigDescription("Proliferator level to boost request items to. 0 to use as-is. 1 boosts all items to level 1 for example",
                    new AcceptableValueRange<int>(0, 3)));
            foreach (var fuelItemProto in LDB.items.dataArray.ToList()
                         .FindAll(i => i.ID > 0 && i.FuelType > 0))
            {
                enabledFuelItems.Add(fuelItemProto.ID,
                    confFile.Bind("Internal", $"FuelType_{fuelItemProto.Name.Translate()}_Enabled", true, $"Allow {fuelItemProto.name} to be used in mecha fuel chamber"));
            }

            configFile = confFile;
            originalButtonPosition = confFile.Bind("Internal", "OriginalButtonPosition", "0,0",
                "Track where the button was before we started messing with it");
            originalButtonSz = confFile.Bind("Internal", "OriginalButtonSz", "0,0",
                "Track button sz before we mess with it");

            showRecycleWindow = confFile.Bind("UI", "ShowRecycleWindow", true,
                "Automatically open a Recycle window whenever inventory is open where items can be dropped in and they will be sent to logistics stations (or stay in buffer if no stations for that item are found)");
            showAmountsInRequestWindow = confFile.Bind("UI", "showAmountsInRequestWindow", true,
                "Add indicators for the currently requested amounts to the request config window");

            testExportOverrideVersion = confFile.Bind("Internal", "TEST Export override version", -1,
                new ConfigDescription("Force an alt version of export to be used",
                    new AcceptableValueRange<int>(-1, SerDeManager.Latest)));
            multiplayerUserId = confFile.Bind("Internal", "Nebula User Id", Guid.NewGuid().ToString(),
                "Don't edit this, it's used to uniquely identify your player in a multiplayer game. If it's changed then your incoming items/desired items/buffer can be lost");
            // force this setting to be -1 so that it has to be set at runtime and can't be left on by accident
            testExportOverrideVersion.Value = -1;
            var languages = Enum.GetNames(typeof(Language)).ToList().FindAll(l => l.ToString().Length == 4);
            languages.Add("");
            testOverrideLanguage = confFile.Bind("Internal", "TEST override language", "",
                new ConfigDescription("Force an alt language to be used (for some text)",
                    new AcceptableValueList<string>(
                        languages.ToArray()
                    )));
            // force this setting to empty so that it has to be set at runtime and can't be left on by accident
            testOverrideLanguage.Value = "";
        }

        public static bool IsPaused()
        {
            return inventoryManagementPaused.Value || NebulaLoadState.instance?.IsWaitingClient() == true ||
                   !GameUtil.IsPlayerGameRunning();
        }

        public static void Play()
        {
            if (!IsPaused())
            {
                Warn($"trying to play but not paused");
            }

            inventoryManagementPaused.Value = false;
        }

        public static void Pause()
        {
            if (IsPaused())
            {
                Warn($"trying to pause but already paused");
            }

            inventoryManagementPaused.Value = true;
        }

        private static Guid _tmpUserGuid = Guid.Empty;

        public static Guid GetAssignedUserId()
        {
            if (multiplayerUserId == null)
            {
                if (_tmpUserGuid == Guid.Empty)
                {
                    Warn($"Using random user id because plugin not yet initted");
                    _tmpUserGuid = Guid.NewGuid();
                }

                return _tmpUserGuid;
            }

            if (Guid.TryParse(multiplayerUserId.Value, out Guid result))
            {
                return result;
            }

            Warn($"failed to get neb user id. Assigning new one. {multiplayerUserId.Value}");
            var newGuid = Guid.NewGuid();
            multiplayerUserId.Value = newGuid.ToString();
            return newGuid;
        }

        public static ItemStack BoostStackProliferator(ItemStack itemStack)
        {
            if (proliferatorPointBoost.Value == 0)
            {
                return itemStack;
            }

            switch (PluginConfig.proliferatorPointBoost.Value)
            {
                case 1:
                {
                    if (itemStack.ItemsAtLevel1() >= itemStack.ItemCount)
                    {
                        return itemStack;
                    }
                    return ItemStack.WithLevels(itemStack.ItemCount, itemStack.ItemCount);
                }
                case 2:
                {
                    if (itemStack.ItemsAtLevel2() >= itemStack.ItemCount)
                    {
                        return itemStack;
                    }
                    return ItemStack.WithLevels(itemStack.ItemCount, 0, itemStack.ItemCount);
                }
                case 3:
                {
                    if (itemStack.ItemsAtLevel3() >= itemStack.ItemCount)
                    {
                        return itemStack;
                    }
                    return ItemStack.WithLevels(itemStack.ItemCount,0,0, itemStack.ItemCount);
                }
            }
            Warn($"Should be unreachable and yet here we are!");
            return itemStack;
        }
        
        public static bool IsItemEnabledForMechaFuelContainer(int fuelItemId)
        {
            if (enabledFuelItems.TryGetValue(fuelItemId, out var fuelEnabled))
            {
                return fuelEnabled.Value;
            }

            // weird
            Warn($"Somehow fuel id {fuelItemId} is not in config list");
            return false;
        }
        
        public static void SetFuelItemState(int selectedItem, bool isOn)
        {
            if (enabledFuelItems.TryGetValue(selectedItem, out var fuelEnabled))
            {
                fuelEnabled.Value = isOn;
            }
            else
            {
                Warn($"Somehow fuel id {selectedItem} {ItemUtil.GetItemName(selectedItem)} is not in config list");
            }
        }
    }
}