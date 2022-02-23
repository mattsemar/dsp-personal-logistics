using System;
using System.IO;
using BepInEx;
using CommonAPI;
using CommonAPI.Systems;
using crecheng.DSPModSave;
using HarmonyLib;
using NebulaAPI;
using PersonalLogistics.Logistics;
using PersonalLogistics.ModPlayer;
using PersonalLogistics.Nebula;
using PersonalLogistics.PlayerInventory;
using PersonalLogistics.Scripts;
using PersonalLogistics.SerDe;
using PersonalLogistics.Shipping;
using PersonalLogistics.UGUI;
using PersonalLogistics.UI;
using PersonalLogistics.Util;
using TMPro;
using UnityEngine;
using static PersonalLogistics.Util.Log;
using Object = UnityEngine.Object;

namespace PersonalLogistics
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInProcess("DSPGAME.exe")]
    [BepInDependency(CommonAPIPlugin.GUID)]
    [BepInDependency(DSPModSavePlugin.MODGUID)]
    [BepInDependency(NebulaModAPI.API_GUID)]
    [BepInDependency(CommonAPIPlugin.LDB_TOOL_GUID)]
    [BepInIncompatibility("semarware.dysonsphereprogram.PersonalLogistics")]
    [CommonAPISubmoduleDependency(nameof(ProtoRegistry), nameof(CustomKeyBindSystem))]
    public class PersonalLogisticsFreePlugin : BaseUnityPlugin, IModCanSave, IMultiplayerMod
    {
        private const string PluginGuid = "semarware.dysonsphereprogram.PersonalLogisticsFree";
        private const string PluginName = "PersonalLogisticsFree";
        private const string PluginVersion = "1.0.0";

        private static float _inventorySyncInterval
        {
            get
            {
                if (PluginConfig.cheatLevel.Value == CheatLevel.Full)
                {
                    return 1.5f;
                }

                return 4.5f;
            }
        }

        private static readonly int VERSION = 2;

        private static PersonalLogisticsFreePlugin instance;
        private Harmony _harmony;
        private bool _initted;
        private float _inventorySyncWaited;
        private RecycleWindow _recycleScript;
        private RequesterWindow _requesterWindow;


        private void Awake()
        {
            logger = Logger;
            instance = this;
            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll(typeof(PersonalLogisticsFreePlugin));
            _harmony.PatchAll(typeof(RequestWindow));
            _harmony.PatchAll(typeof(RecycleWindow));
            _harmony.PatchAll(typeof(RequesterWindow));
            RegisterKeyBinds();
            Strings.Init();
            PluginConfig.InitConfig(Config);
            _recycleScript = gameObject.AddComponent<RecycleWindow>();
            Asset.Init(PluginGuid, "pui");
            PlogPlayerRegistry.ClearLocal();
            NebulaLoadState.Register();
            Log.Info($"{PluginName} Plugin Loaded {PluginVersion}");
        }


        private void Update()
        {
            if (GameMain.mainPlayer == null || UIRoot.instance == null || UIRoot.instance.uiGame == null ||
                UIRoot.instance.uiGame.globemap == null)
            {
                return;
            }

            if (!GameMain.isRunning)
            {
                return;
            }

            NebulaLoadState.instance.RequestStateFromHost();

            if (!LogisticsNetwork.IsInitted)
            {
                Debug("Starting logistics network");
                LogisticsNetwork.Start();
                if (!_initted)
                {
                    InitUi();
                }
            }

            if (VFInput.control && Input.GetKeyDown(KeyCode.F3))
            {
                if (GameMain.data.trashSystem != null)
                {
                    TrashHandler.trashSystem = GameMain.data.trashSystem;
                    TrashHandler.player = GameMain.mainPlayer;
                }

                foreach (var itemProto in ItemUtil.GetAllItems())
                {
                    TrashHandler.AddTask(itemProto.ID);
                }

                UIRoot.ClearFatalError();
                GameMain.errored = false;
                var parent = GameObject.Find("UI Root/Overlay Canvas/In Game/");
                var componentsInParent = parent.GetComponentsInChildren<UIItemTip>();
                logger.LogDebug($"found {componentsInParent.Length} tip windows to close");

                return;
            }

            if (PluginConfig.IsPaused())
            {
                return;
            }

            PlogPlayerRegistry.LocalPlayer()?.inventoryManager.ProcessInventoryActions();
            if (_inventorySyncWaited < _inventorySyncInterval && LogisticsNetwork.IsInitted &&
                LogisticsNetwork.IsFirstLoadComplete)
            {
                _inventorySyncWaited += Time.deltaTime;
                if (_inventorySyncWaited >= _inventorySyncInterval)
                {
                    PlogPlayerRegistry.LocalPlayer()?.personalLogisticManager.SyncInventory();
                }
            }
            else
            {
                _inventorySyncWaited = 0.0f;
            }

            if (Time.frameCount % 105 == 0 && LogisticsNetwork.IsInitted && LogisticsNetwork.IsFirstLoadComplete)
            {
                TrashHandler.ProcessTasks();
                ShippingManager.Process();
            }
        }


        public void OnGUI()
        {
            if (RequestWindow.Visible)
            {
                RequestWindow.OnGUI();
            }

            if (_requesterWindow == null && GameMain.isRunning && GameMain.mainPlayer != null && !DSPGame.IsMenuDemo)
            {
                _requesterWindow = gameObject.AddComponent<RequesterWindow>();
            }
        }

        public void Export(BinaryWriter w)
        {
            if (PluginConfig.testExportOverrideVersion.Value > 0)
            {
                SerDeManager.Export(w, PluginConfig.testExportOverrideVersion.Value);
            }
            else
            {
                SerDeManager.Export(w);
            }
        }

        public void Import(BinaryReader r)
        {
            SerDeManager.Import(r);
        }

        public void IntoOtherSave()
        {
            PlogPlayerRegistry.RegisterLocal(PlogPlayerId.ComputeLocalPlayerId());
            RecycleWindow.InitOnLoad();
        }

        private void InitUi()
        {
            var buttonToCopy = UIRoot.instance.uiGame.gameMenu.button4;
            if (buttonToCopy == null || buttonToCopy.gameObject.GetComponent<RectTransform>() == null)
            {
                return;
            }

            var rectTransform = buttonToCopy.gameObject.GetComponent<RectTransform>();
            var newButton = Pui.CopyButton(rectTransform,
                Vector2.left * 35
                + Vector2.down * 3,
                Asset.LoadIconSprite(),
                v =>
                {
                    if (_requesterWindow != null)
                    {
                        _requesterWindow.Toggle();
                    }
                });

            if (UIRoot.instance.uiGame.inventory != null && newButton != null)
            {
                _initted = true;
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(GameMain), "End")]
        public static void OnGameEnd()
        {
            LogisticsNetwork.Stop();
            if (instance != null && instance._recycleScript != null)
            {
                instance._recycleScript.Unload(false);
            }

            NebulaLoadState.Reset();
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(GameMain), "Start")]
        public static void OnGameStart()
        {
            NebulaLoadState.instance = new NebulaLoadState();
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(TrashSystem), "AddTrash")]
        public static void TrashSystem_AddTrash_Postfix(TrashSystem __instance, int itemId, int count, int objId)
        {
            if (instance == null || !PluginConfig.sendLitterToLogisticsNetwork.Value)
            {
                return;
            }

            if (!LogisticsNetwork.IsInitted || !LogisticsNetwork.IsFirstLoadComplete)
            {
                return;
            }

            if (!LogisticsNetwork.HasItem(itemId))
            {
                return;
            }

            TrashHandler.trashSystem = __instance;
            TrashHandler.player = __instance.player;
            TrashHandler.AddTask(itemId);
        }

        private void RegisterKeyBinds()
        {
            if (!CustomKeyBindSystem.HasKeyBind("ShowPlogFreeWindow"))
                CustomKeyBindSystem.RegisterKeyBind<PressKeyBind>(new BuiltinKey
                {
                    id = 241,
                    key = new CombineKey((int) KeyCode.E, CombineKey.CTRL_COMB, ECombineKeyAction.OnceClick, false),
                    conflictGroup = 2052,
                    name = "ShowPlogFreeWindow",
                    canOverride = true
                });
            else
            {
                Warn("KeyBind with ID=241, ShowPlogFreeWindow already bound");
            }
        }

        [HarmonyPatch(typeof(Resources), "Load", typeof(string), typeof(Type))]
        [HarmonyPrefix]
        public static bool Prefix(ref string path, Type systemTypeInstance, ref Object __result)
        {
            if (path.Contains("TMP Settings"))
            {
                Debug($"intercepting call for {path}");
                var asset = Asset.bundle.LoadAsset<TMP_Settings>("Assets/TextMesh Pro/Resources/TMP Settings.asset");
                if (asset != null)
                {
                    __result = asset;
                    Debug("successfully loaded asset");
                    return false;
                }

                Warn($"failed to load asset, still null");
            }

            return true;
        }

        public bool CheckVersion(string hostVersion, string clientVersion)
        {
            return hostVersion.Equals(clientVersion);
        }

        public string Version => PluginVersion;
    }
}