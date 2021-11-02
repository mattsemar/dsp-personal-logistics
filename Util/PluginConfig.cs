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
        public static ConfigEntry<bool> useMechaEnergyOnly;
        public static ConfigEntry<bool> enableCopyGame;

        public static ConfigEntry<string> originalButtonPosition;
        public static ConfigEntry<string> originalButtonSz;

        public static ConfigEntry<bool> showIncomingItemProgress;
        public static ConfigEntry<bool> showNearestBuildGhostIndicator;

        public static ConfigFile configFile { get; private set; }


        public static void InitConfig(ConfigFile confFile)
        {
            if (configFile != null)
                return;
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

            Debug($"InitConfig");
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

            showIncomingItemProgress = confFile.Bind("UI", "ShowIncomingItemProgress", true,
                "Show indicator for items entering inventory soon");
            showNearestBuildGhostIndicator = confFile.Bind("UI", "ShowNearestBuildGhostIndicator", true,
                "Show indicator with count and coords for build ghosts, components that haven't been created yet by bots");
            enableCopyGame = confFile.Bind("UI", "Enable Copy Game", false,
                "Add buttons for copying desired inventory state from another seed");
        }

        public static bool Initted()
        {
            return configFile != null;
        }
    }
}