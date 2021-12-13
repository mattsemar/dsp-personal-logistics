using System;
using System.ComponentModel;
using BepInEx.Configuration;
using static PersonalLogistics.Util.Log;

namespace PersonalLogistics.Util
{
    public enum StationSourceMode
    {
        [Description("Take items from any station that has it, regardless of supply or demand")]
        All,

        [Description("Take items from any station with item set to: Supply (PLS), Remote Supply, Local Supply")]
        AnySupply,

        [Description("Follow the same rules as a nearby ILS with Remote Demand/Local Demand set")]
        IlsDemandRules,

        [Description("Same as IlsDemandRules but also take from PLS on other planets set to Supply")]
        IlsDemandWithPls
    }

    public class PluginConfig
    {
        public static ConfigEntry<string> crossSeedInvState;

        public static ConfigEntry<StationSourceMode> stationRequestMode;
        public static ConfigEntry<bool> sortInventory;
        public static ConfigEntry<bool> inventoryManagementPaused;
        public static ConfigEntry<bool> sendLitterToLogisticsNetwork;
        public static ConfigEntry<int> maxWaitTimeInSeconds;
        public static ConfigEntry<int> minRecycleDelayInSeconds;
        public static ConfigEntry<bool> useMechaEnergyOnly;
        public static ConfigEntry<bool> enableCopyGame;

        public static ConfigEntry<string> originalButtonPosition;
        public static ConfigEntry<string> originalButtonSz;
        public static ConfigEntry<bool> timeScriptPositionTestEnabled;

        public static ConfigEntry<bool> showIncomingItemProgress;
        public static ConfigEntry<bool> showRecycleWindow;
        public static ConfigEntry<bool> useLegacyRequestWindowUI;
        public static ConfigEntry<bool> addFuelToMecha;
        public static ConfigEntry<bool> addWarpersToMecha;

        public static ConfigFile configFile { get; private set; }


        public static void InitConfig(ConfigFile confFile)
        {
            if (configFile != null)
            {
                return;
            }

            stationRequestMode = confFile.Bind("Logistics", "Station Request Mode", StationSourceMode.All,
                "Limit which stations to take items from");

            sortInventory = confFile.Bind("Inventory", "SortInventory", true,
                "Enable/disable sorting of inventory after items are added/removed");
            inventoryManagementPaused = confFile.Bind("Inventory", "InventoryManagementPaused", false,
                new ConfigDescription("Temporarily pause management of player inventory", null, "configEditOnly"));
            sendLitterToLogisticsNetwork = confFile.Bind("Inventory", "SendLitterToLogisticsNetwork", true,
                "Use personal logistics system to send littered items to nearby logistics stations");
            useMechaEnergyOnly = confFile.Bind("Inventory", "UseMechaEnergyOnly", false,
                "Always use energy from mecha to power personal logistics drones");
            maxWaitTimeInSeconds = confFile.Bind("Inventory", "Max Wait Time In Seconds", 600,
                new ConfigDescription("Max time to wait for items to be delivered. If calculated arrival time is more than this value, item request will be canceled",
                    new AcceptableValueRange<int>(10, 25_000)));
            addFuelToMecha = confFile.Bind("Inventory", "Add fuel to mecha fuel chamber", false, "Add fuel from inventory to mecha, any usable fuel found will be used");
            addWarpersToMecha = confFile.Bind("Inventory", "Add warpers to mecha", false,
                "Add warpers from inventory to mecha, requires that warpers be available in Logistics Network and currently requested");
            minRecycleDelayInSeconds = confFile.Bind("Inventory", "Min Recycle Delay Seconds", 10,
                new ConfigDescription("Minimum wait time before items in recycle area are removed",
                    new AcceptableValueRange<int>(0, 100)));

            Debug("InitConfig");
            try
            {
                crossSeedInvState = confFile.Bind("Internal", "CrossSeedInvState", "",
                    new ConfigDescription("Edit at your own risk, stores the desired inventory between reloads",
                        null, "configEditOnly"));
                configFile = confFile;
            }
            catch (Exception e)
            {
                Warn($"Exception in initConfig {e}");
            }

            originalButtonPosition = confFile.Bind("Internal", "OriginalButtonPosition", "0,0",
                "Track where the button was before we started messing with it");
            originalButtonSz = confFile.Bind("Internal", "OriginalButtonSz", "0,0",
                "Track button sz before we mess with it");
            timeScriptPositionTestEnabled = confFile.Bind("Internal", "IncomingItemPositionTest", false,
                "Test position of incoming items text");

            showIncomingItemProgress = confFile.Bind("UI", "ShowIncomingItemProgress", true,
                "Show indicator for items entering inventory soon");
            showRecycleWindow = confFile.Bind("UI", "ShowRecycleWindow", true,
                "Automatically open a Recycle window whenever inventory is open where items can be dropped in and they will be sent to logistics stations (or stay in buffer if no stations for that item are found)");
            useLegacyRequestWindowUI = confFile.Bind("UI", "useLegacyRequestWindowUI", false,
                "Revert to legacy request window UI.");

            enableCopyGame = confFile.Bind("UI", "Enable Copy Game", false,
                "Add buttons for copying desired inventory state from another seed");
        }

        public static bool Initted() => configFile != null;
    }
}