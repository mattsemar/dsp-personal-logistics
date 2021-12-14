using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using CommonAPI;
using CommonAPI.Systems;
using crecheng.DSPModSave;
using HarmonyLib;
using PersonalLogistics.Logistics;
using PersonalLogistics.Model;
using PersonalLogistics.PlayerInventory;
using PersonalLogistics.Scripts;
using PersonalLogistics.Shipping;
using PersonalLogistics.UGUI;
using PersonalLogistics.UI;
using PersonalLogistics.Util;
using UnityEngine;
using static PersonalLogistics.Util.Log;
using Debug = UnityEngine.Debug;

namespace PersonalLogistics
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInProcess("DSPGAME.exe")]
    [BepInDependency(CommonAPIPlugin.GUID)]
    [BepInDependency(DSPModSavePlugin.MODGUID)]
    [CommonAPISubmoduleDependency(nameof(ProtoRegistry), nameof(CustomKeyBindSystem))]
    public class PersonalLogisticsPlugin : BaseUnityPlugin, IModCanSave
    {
        private const string PluginGuid = "semarware.dysonsphereprogram.PersonalLogistics";
        private const string PluginName = "PersonalLogistics";
        private const string PluginVersion = "2.0.4";
        private const float InventorySyncInterval = 4.5f;
        private static readonly int VERSION = 2;

        private static PersonalLogisticsPlugin instance;
        private readonly List<GameObject> _objectsToDestroy = new List<GameObject>();
        private Harmony _harmony;
        private bool _initted;
        private float _inventorySyncWaited;
        private RecycleWindow _recycleScript;
        private RequesterWindow _requesterWindow;

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
            Debug.Log($"PersonalLogistics Plugin Loaded (plugin folder {FileUtil.GetBundleFilePath()})");
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
            if (PluginConfig.inventoryManagementPaused.Value)
            {
                return;
            }

            if (InventoryManager.instance != null)
            {
                InventoryManager.instance.ProcessInventoryActions();
            }


            if (_inventorySyncWaited < InventorySyncInterval && LogisticsNetwork.IsInitted && LogisticsNetwork.IsFirstLoadComplete)
            {
                _inventorySyncWaited += Time.deltaTime;
                if (_inventorySyncWaited >= InventorySyncInterval)
                {
                    PersonalLogisticManager.SyncInventory();
                }
            }
            else
            {
                _inventorySyncWaited = 0.0f;
            }

            if (Time.frameCount % 105 == 0)
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
                    _timeScript.Unload();
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
                _timeScript = gameObject.AddComponent<TimeScript>();
            }

            if (_requesterWindow == null && GameMain.isRunning  && GameMain.mainPlayer != null && !DSPGame.IsMenuDemo)
            {
                _requesterWindow = gameObject.AddComponent<RequesterWindow>();
            }
        }

        public void Export(BinaryWriter w)
        {
            w.Write(VERSION);
            PersonalLogisticManager.Export(w);
            ShippingManager.Export(w);
            DesiredInventoryState.Export(w);
            RecycleWindow.Export(w);
        }

        public void Import(BinaryReader r)
        {
            var ver = r.ReadInt32();
            if (ver != VERSION)
            {
                Debug($"version from save: {ver} does not match mod version: {VERSION}");
            }

            PersonalLogisticManager.Import(r);
            ShippingManager.Import(r);
            if (ver > 1)
            {
                // we can just read the desired inventory state from save file
                Debug($"Loading desired inv state from modsave");
                DesiredInventoryState.Import(r);
                RecycleWindow.Import(r);
            }
            else
            {
                // have to get desired inventory state from config  
                var desiredInventoryState = DesiredInventoryState.Instance;
                if (desiredInventoryState != null)
                {
                    Debug("migrated desired inv state from config property");
                }
                else
                {
                    Warn("Failed to migrate desired inventory state");
                }
                // recycle window state is not available to us
            }
        }

        public void IntoOtherSave()
        {
            PersonalLogisticManager.InitOnLoad();
            ShippingManager.InitOnLoad();

            var desiredInventoryState = DesiredInventoryState.Instance;
            if (desiredInventoryState != null)
            {
                Debug("initialized desired inventory save");
            }
            else
            {
                Warn("Failed to migrate desired inventory state");
            }

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
                LoadFromFile.LoadIconSprite(),
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

            if (UIRoot.instance.uiGame.inventory != null && newButton != null)
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
                UINetworkStatusTip.Create(__instance);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(GameMain), "End")]
        public static void OnGameEnd()
        {
            LogisticsNetwork.Stop();
            InventoryManager.Reset();
            ShippingManager.Reset();
            if (instance != null && instance._recycleScript != null)
            {
               instance._recycleScript.Unload(false);
            }
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
    }
}