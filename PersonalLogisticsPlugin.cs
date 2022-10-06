using System;
using System.Collections.Generic;
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
using xiaoye97;
using static PersonalLogistics.Util.Log;
using Object = UnityEngine.Object;

namespace PersonalLogistics
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInProcess("DSPGAME.exe")]
    [BepInDependency(CommonAPIPlugin.GUID)]
    [BepInDependency(DSPModSavePlugin.MODGUID)]
    [BepInDependency(NebulaModAPI.API_GUID)]
    [BepInDependency(LDBToolPlugin.MODGUID)]
    [BepInDependency(DSPModSavePlugin.MODGUID)]
    [CommonAPISubmoduleDependency(nameof(ProtoRegistry), nameof(CustomKeyBindSystem), nameof(TabSystem))]
    public class PersonalLogisticsPlugin : BaseUnityPlugin, IModCanSave, IMultiplayerMod
    {
        private const string PluginGuid = "semarware.dysonsphereprogram.PersonalLogistics";
        private const string PluginName = "PersonalLogistics";
        private const string PluginVersion = "2.9.8";
        private const float InventorySyncInterval = 4.5f;
        private static readonly int VERSION = 2;

        private static PersonalLogisticsPlugin instance;
        private readonly List<GameObject> _objectsToDestroy = new();
        private Harmony _harmony;
        private bool _initted;
        private float _inventorySyncWaited;
        private RecycleWindow _recycleScript;
        private RequesterWindow _requesterWindow;
        private Object _exportLock = new();

        private TimeScript _timeScript;


        private void Awake()
        {
            logger = Logger;
            instance = this;
            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll(typeof(PersonalLogisticsPlugin));
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
#if DEBUG
            gameObject.AddComponent<TestPersistence>();
#else
            Log.Debug("Release build");
#endif
            Log.Info($"PersonalLogistics Plugin Loaded {PluginVersion}");
        }


        private void Update()
        {
            if (GameMain.mainPlayer == null || UIRoot.instance == null || UIRoot.instance.uiGame == null || UIRoot.instance.uiGame.globemap == null)
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
                foreach (var tipWindow in componentsInParent)
                {
                    if (UINetworkStatusTip.IsOurTip(tipWindow))
                    {
                        UINetworkStatusTip.CloseTipWindow(tipWindow);
                    }
                    else
                    {
                        Destroy(tipWindow.gameObject);
                    }
                }

                return;
            }

            UINetworkStatusTip.UpdateAll();
            if (PluginConfig.IsPaused())
            {
                return;
            }

            PlogPlayerRegistry.LocalPlayer()?.inventoryManager.ProcessInventoryActions();
            if (_inventorySyncWaited < InventorySyncInterval && LogisticsNetwork.IsInitted && LogisticsNetwork.IsFirstLoadComplete)
            {
                _inventorySyncWaited += Time.deltaTime;
                if (_inventorySyncWaited >= InventorySyncInterval)
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


        private void OnDestroy()
        {
            LogisticsNetwork.Stop();
            foreach (var gameObj in _objectsToDestroy)
            {
                try
                {
                    Destroy(gameObj);
                }
                catch (Exception e)
                {
                    Warn($"failed to destroy gameobject {e.Message}\n{e.StackTrace}");
                }
            }

            try
            {
                Pui.Unload();
                if (_timeScript != null && _timeScript.gameObject != null)
                {
                    Destroy(_timeScript.gameObject);
                    _timeScript = null;
                }

                if (_recycleScript != null && _recycleScript.gameObject != null)
                {
                    _recycleScript.Unload(true);
                    Destroy(_recycleScript.gameObject);
                    _recycleScript = null;
                }

                if (_requesterWindow != null && _requesterWindow.gameObject != null)
                {
                    _requesterWindow.Unload();
                    Destroy(_requesterWindow.gameObject);
                    _requesterWindow = null;
                }
            }
            catch (Exception e)
            {
                Warn($"something went wrong unloading timescript {e.Message}\r\n{e.StackTrace}");
            }

            _objectsToDestroy.Clear();
            _harmony.UnpatchSelf();
        }

        public void OnGUI()
        {
            if (RequestWindow.Visible)
            {
                RequestWindow.OnGUI();
            }

            if (_timeScript == null && GameMain.isRunning && LogisticsNetwork.IsInitted && GameMain.mainPlayer != null)
            {
                var prefab = Asset.bundle.LoadAsset<GameObject>("Assets/prefab/Incoming items v2.prefab");
                var inGameGo = GameObject.Find("UI Root/Overlay Canvas/In Game");
                var prefabTs = Instantiate(prefab, inGameGo.transform, false);
                _timeScript = prefabTs.GetComponent<TimeScript>();

                // make sure the arrival time stuff appears behind inventory window and the UIItemUp stuff
                _timeScript.transform.SetAsFirstSibling();
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
                    if (!PluginConfig.useLegacyRequestWindowUI.Value)
                    {
                        if (_requesterWindow != null)
                        {
                            _requesterWindow.Toggle();
                        }
                    }
                    else
                    {
                        RequestWindow.Visible = !RequestWindow.Visible;
                    }
                });
            if (newButton != null)
            {
                _objectsToDestroy.Add(newButton.gameObject);
            }

            if (UIRoot.instance.uiGame.inventoryWindow != null && newButton != null)
            {
                _initted = true;
            }
        }


        [HarmonyPostfix]
        [HarmonyPatch(typeof(UIItemTip), "SetTip")]
        public static void UIItemTip_SetTip_Postfix(UIItemTip __instance)
        {
            if (__instance != null && __instance.descText.text != null && instance != null && LogisticsNetwork.IsInitted && !UINetworkStatusTip.IsOurTip(__instance))
            {
                if (PluginConfig.showItemTooltips.Value)
                    UINetworkStatusTip.Create(__instance);
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
        [HarmonyPatch(typeof(GameData), nameof(GameData.LeavePlanet))]
        public static void OnLeavePlanet()
        {
            PlogPlayerRegistry.LocalPlayer()?.NotifyLeavePlanet();
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
            if (!CustomKeyBindSystem.HasKeyBind("ShowPlogWindow"))
                CustomKeyBindSystem.RegisterKeyBind<PressKeyBind>(new BuiltinKey
                {
                    id = 211,
                    key = new CombineKey((int)KeyCode.E, CombineKey.CTRL_COMB, ECombineKeyAction.OnceClick, false),
                    conflictGroup = 2052,
                    name = "ShowPlogWindow",
                    canOverride = true
                });
            else
            {
                Warn("KeyBind with ID=211, ShowPlogWindow already bound");
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