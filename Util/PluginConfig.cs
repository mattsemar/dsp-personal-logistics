using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using BepInEx.Configuration;
using PersonalLogistics.Nebula;
using PersonalLogistics.SerDe;
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
        IlsDemandWithPls,

        [Description("Only interact with logistics stations on the same planet as the player. Follow the same rules as a nearby PLS set to Demand")]
        Planetary
    }

    public enum PlanetarySourceMode
    {
        [Description(
            "When you go to a new planet, buffered items can still be used to refill inventory. So, as you use items on the new planet, your inventory will be replenished from your buffer until empty")]
        Unrestricted,

        [Description("When you go to a new planet, buffered items can still be used to refill inventory, but <b>only</b> if the item is also available on the new planet")]
        OnlyLocallyAvailable,

        [Description("When you leave a planet, all buffered items are returned to the logistics network")]
        ReturnBufferOnDepart
    }

    public class PluginConfig
    {
        public static ConfigEntry<string> crossSeedInvState;

        public static ConfigEntry<StationSourceMode> stationRequestMode;
        public static ConfigEntry<PlanetarySourceMode> planetarySourceMode;
        public static ConfigEntry<int> warpEnableMinAu;
        public static ConfigEntry<bool> sortInventory;
        public static ConfigEntry<bool> inventoryManagementPaused;
        public static ConfigEntry<bool> playerConfirmedTrash;
        public static ConfigEntry<bool> sendLitterToLogisticsNetwork;
        public static ConfigEntry<int> maxWaitTimeInSeconds;
        public static ConfigEntry<int> minRecycleDelayInSeconds;
        public static ConfigEntry<bool> useMechaEnergyOnly;
        public static ConfigEntry<bool> neverUseMechaEnergy;
        public static ConfigEntry<bool> neverUseMechaWarper;
        public static ConfigEntry<int> minStacksToLoadFromStations;

        public static ConfigEntry<string> originalButtonPosition;
        public static ConfigEntry<string> originalButtonSz;
        public static ConfigEntry<bool> timeScriptPositionTestEnabled;

        public static ConfigEntry<bool> showIncomingItemProgress;
        public static ConfigEntry<bool> hideIncomingItemFailures;
        public static ConfigEntry<bool> showRecycleWindow;
        public static ConfigEntry<bool> useLegacyRequestWindowUI;
        public static ConfigEntry<bool> showAmountsInRequestWindow;
        public static ConfigEntry<bool> showItemTooltips;
        public static ConfigEntry<bool> addFuelToMecha;
        public static Dictionary<int, ConfigEntry<bool>> enabledFuelItems = new();
        public static ConfigEntry<bool> addWarpersToMecha;

        public static ConfigEntry<int> testExportOverrideVersion;
        public static ConfigEntry<string> testOverrideLanguage;
        public static ConfigEntry<string> multiplayerUserId;

        public static ConfigFile configFile { get; private set; }
#if DEBUG
        public static ConfigEntry<double> overriddenTransitTimeSeconds;
#endif


        public static void InitConfig(ConfigFile confFile)
        {
            if (configFile != null)
            {
                return;
            }

            stationRequestMode = confFile.Bind("Logistics", "Station Request Mode", StationSourceMode.IlsDemandRules,
                "Limit which stations to take items from");
            planetarySourceMode = confFile.Bind("Logistics", "Planet Source Mode", PlanetarySourceMode.Unrestricted,
                "Additional controls for Planetary source mode" +
                " If unset with PlsDemandRules mode, items will not be loaded from local buffer if they are not also available nearby");
            warpEnableMinAu = confFile.Bind("Logistics", "Warp Enable Min AU", 1,
                new ConfigDescription("Set a minimum in AU (1 AU = 40 KM, 1 LY = 60 AU) where warpers will be required before shipping will be attempted\r\n" +
                                      "Example: Value = 1 then any shipping over 40 KM will use a warper and won't be processed until warpers are available\r\n" +
                                      "Example: Value = 60, any shipping under 1 LY will not use warpers and can be very slow\r\n" +
                                      "If this value is set to 0 then each station's 'Distance To Enable Warp' value will be used",
                    new AcceptableValueRange<int>(0, 60)));

            sortInventory = confFile.Bind("Inventory", "SortInventory", true,
                "Enable/disable sorting of inventory after items are added/removed");
            inventoryManagementPaused = confFile.Bind("Inventory", "InventoryManagementPaused", false,
                new ConfigDescription("Temporarily pause management of player inventory", null, "configEditOnly"));
            playerConfirmedTrash = confFile.Bind("Inventory", "Player Confirmed Trash Recycler", false,
                new ConfigDescription("Stores whether the player has confirmed that they are aware that littered items will be sent to logistics network", null, "configEditOnly"));
            sendLitterToLogisticsNetwork = confFile.Bind("Inventory", "SendLitterToLogisticsNetwork", true,
                "Use personal logistics system to send littered items to nearby logistics stations");
            useMechaEnergyOnly = confFile.Bind("Inventory", "UseMechaEnergyOnly", false,
                "Always use energy from mecha to power personal logistics drones");
            neverUseMechaEnergy = confFile.Bind("Inventory", "Never Use Mecha Energy", false,
                "Never use energy from mecha, only use station energy. Don't set this and UseMechaEnergyOnly or no shipping will happen");
            neverUseMechaWarper = confFile.Bind("Inventory", "Never Use Mecha Warper", false,
                "Never use warpers from mecha, use only from stations. Be careful, this can lead to some items never being shipped if their stations don't have warpers");
            minStacksToLoadFromStations = confFile.Bind("Inventory", "Min Stacks To Load", 0,
                new ConfigDescription("Don't load items from stations if there are not at least this many stacks of the item available", new AcceptableValueRange<int>(0, 10)));
            maxWaitTimeInSeconds = confFile.Bind("Inventory", "Max Wait Time In Seconds", 600,
                new ConfigDescription("Max time to wait for items to be delivered. If calculated arrival time is more than this value, item request will be canceled",
                    new AcceptableValueRange<int>(10, 25_000)));
            addFuelToMecha = confFile.Bind("Inventory", "Add fuel to mecha fuel chamber", false, "Add fuel from inventory to mecha, any usable fuel found will be used");
            addWarpersToMecha = confFile.Bind("Inventory", "Add warpers to mecha", false,
                "Add warpers from inventory to mecha, requires that warpers be available in Logistics Network and currently requested");
            minRecycleDelayInSeconds = confFile.Bind("Inventory", "Min Recycle Delay Seconds", 10,
                new ConfigDescription("Minimum wait time before items in recycle area are removed",
                    new AcceptableValueRange<int>(0, 100)));
            foreach (var fuelItemProto in LDB.items.dataArray.ToList()
                         .FindAll(i => i.ID > 0 && i.FuelType > 0))
            {
                enabledFuelItems.Add(fuelItemProto.ID,
                    confFile.Bind("Internal", $"FuelType_{fuelItemProto.Name.Translate()}_Enabled", true, $"Allow {fuelItemProto.name} to be used in mecha fuel chamber"));
            }

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
            hideIncomingItemFailures = confFile.Bind("UI", "HideIncomingItemFailures", false,
                "Suppress failure messages for incoming items that were not able to be loaded from logistics network");
            showRecycleWindow = confFile.Bind("UI", "ShowRecycleWindow", true,
                "Automatically open a Recycle window whenever inventory is open where items can be dropped in and they will be sent to logistics stations (or stay in buffer if no stations for that item are found)");
            useLegacyRequestWindowUI = confFile.Bind("UI", "useLegacyRequestWindowUI", false,
                "Revert to legacy request window UI.");
            showAmountsInRequestWindow = confFile.Bind("UI", "showAmountsInRequestWindow", true,
                "Add indicators for the currently requested amounts to the request config window");
            showItemTooltips = confFile.Bind("UI", "showItemTooltips", true,
                "Add tooltips when items are hovered showing the number available in the logistics network.");

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
            timeScriptPositionTestEnabled.Value = false;

#if DEBUG
            overriddenTransitTimeSeconds = confFile.Bind("Internal", "TEST override transit time seconds", 0.0D,
                "for debug builds set to more than 0 to make shipping cost calculator always return this value ");
#endif
        }

        public static bool IsPaused()
        {
            return inventoryManagementPaused.Value || NebulaLoadState.instance?.IsWaitingClient() == true || !GameUtil.IsPlayerGameRunning();
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

        public static Guid RegenerateAssignedUserId()
        {
            Warn($"Assigning new player id {multiplayerUserId.Value}");
            var newGuid = Guid.NewGuid();
            multiplayerUserId.Value = newGuid.ToString();
            return newGuid;
        }

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

        public static double GetMinWarpDistanceMeters(double stationInfoWarpEnableDistance)
        {
            return warpEnableMinAu.Value == 0 ? stationInfoWarpEnableDistance : warpEnableMinAu.Value * (double) 40_000;
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