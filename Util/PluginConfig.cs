using System;
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
        [Description("Buffered items can still be used to refill inventory (only applies to Planetary source mode)")]
        Unrestricted,

        [Description("Buffered items only used to refill inventory if available in nearby PLS (only applies to Planetary source mode)")]
        OnlyLocallyAvailable,

        [Description("Return buffered items to logistics network when you leave the planet (only applies to Planetary source mode)")]
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
        public static ConfigEntry<bool> addFuelToMecha;
        public static ConfigEntry<bool> addWarpersToMecha;

        public static ConfigEntry<int> testExportOverrideVersion;
        public static ConfigEntry<bool> testLogUiStationWindow;
        public static ConfigEntry<string> testOverrideLanguage;
        public static ConfigEntry<string> multiplayerUserId;

        public static ConfigFile configFile { get; private set; }


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
                new ConfigDescription("Set a minimum in AU (40 KM, 60 AU = 1 LY) where warpers will be required before shipping will be attempted\r\n" +
                                      "Note, that if supplying station uses a higher value than this, the stations value will be used instead\r\n" +
                    "Example: Value = 1 then any shipping over 40 KM will use a warper and won't be processed until warpers are available\r\n" +
                    "Example: Value = 60, any shipping under 1 LY will not use warpers and can be very slow\r\n" +
                    "If this value is set to 0 then each station's Distance To Enable Warp will be used",
                    new AcceptableValueRange<int>(0, 60)));
            // warpRequiredAu = confFile.Bind("Logistics", "Warp Required AU", 1,
            //     new ConfigDescription("Set a distance above which warpers must be used. Values less than 'Warp Enable Min AU' will just use ",
            //         new AcceptableValueRange<int>(0, 120)));
                

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

            testExportOverrideVersion = confFile.Bind("Internal", "TEST Export override version", -1,
                new ConfigDescription("Force an alt version of export to be used",
                    new AcceptableValueRange<int>(-1, SerDeManager.Latest)));
            testLogUiStationWindow = confFile.Bind("Internal", "TEST Station Window values", false, "Log message for station when opened");
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

        public static Guid GetAssignedUserId()
        {
            if (Guid.TryParse(multiplayerUserId.Value, out Guid result))
            {
                return result;
            }

            Warn($"failed to get neb user id. Assigning new one. {multiplayerUserId.Value}");
            var newGuid = Guid.NewGuid();
            multiplayerUserId.Value = newGuid.ToString();
            return newGuid;
        }

        public static double GetMinWarpDistanceMeters()
        {
            return warpEnableMinAu.Value * 40_000;
        }
    }
}