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
        }

        public static bool Initted()
        {
            return _configFile != null;
        }
    }
}