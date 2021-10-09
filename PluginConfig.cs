using System;
using BepInEx.Configuration;
using static PersonalLogistics.Log;

namespace PersonalLogistics
{
    public class PluginConfig
    {
        public static ConfigEntry<string> crossSeedInvState;
        public static ConfigEntry<bool> sortInventory;
        public static ConfigEntry<bool> inventoryManagementPaused;
        public static ConfigEntry<bool> sendLitterToLogisticsNetwork;
        public static ConfigEntry<bool> useMechaEnergyOnly;
        public static ConfigEntry<string> originalButtonPosition;
        public static ConfigEntry<string> originalButtonSz;

        private static ConfigFile _configFile;


        public static void InitConfig(ConfigFile configFile)
        {
            if (_configFile != null)
                return;
            sortInventory = configFile.Bind("Inventory", "SortInventory", true,
                "Enable/disable sorting of inventory after items are added/removed");
            inventoryManagementPaused = configFile.Bind("Inventory", "InventoryManagementPaused", false,
                "Temporarily pause management of player inventory");
            sendLitterToLogisticsNetwork = configFile.Bind("Inventory", "SendLitterToLogisticsNetwork", true,
                "Use personal logistics system to send littered items to nearby logistics stations");
            useMechaEnergyOnly = configFile.Bind("Inventory", "UseMechaEnergyOnly", false,
                "Always use energy from mecha to power personal logistics drones");
            Debug($"InitConfig");
            try
            {
                crossSeedInvState = configFile.Bind("Internal", "CrossSeedInvState", "",
                    new ConfigDescription("Edit at your own risk, stores the desired inventory between reloads",
                        null, "configEditOnly"));
                _configFile = configFile;
            }
            catch (Exception e)
            {
                Warn($"Exception in initConfig {e}");
            }
            originalButtonPosition = configFile.Bind("Internal", "OriginalButtonPosition", "0,0",
                "Track where the button was before we started messing with it");
            originalButtonSz = configFile.Bind("Internal", "OriginalButtonSz", "0,0",
                "Track button sz before we mess with it");

        }

        public static bool Initted()
        {
            return _configFile != null;
        }
    }
}