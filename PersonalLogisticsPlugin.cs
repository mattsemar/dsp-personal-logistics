using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using CommonAPI;
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
    public class PersonalLogisticsPlugin : BaseUnityPlugin, IModCanSave
    {
        private const string PluginGuid = "semarware.dysonsphereprogram.PersonalLogistics";
        private const string PluginName = "PersonalLogistics";
        private const string PluginVersion = "1.6.2";
        private const float InventorySyncInterval = 4.5f;
        private static readonly int VERSION = 1;

        private static PersonalLogisticsPlugin instance;
        private readonly List<GameObject> _objectsToDestroy = new List<GameObject>();
        private Harmony _harmony;
        private bool _initted;
        private float _inventorySyncWaited;
        private RecycleWindow _recycleScript;

        private TimeScript _timeScript;

        private void Awake()
        {
            logger = Logger;
            instance = this;
            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll(typeof(PersonalLogisticsPlugin));
            _harmony.PatchAll(typeof(RequestWindow));
            _harmony.PatchAll(typeof(RecycleWindow));
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
                PluginConfig.InitConfig(Config);
                ShippingManager.Init();

                Debug("Starting logistics network");
                LogisticsNetwork.Start();
                CrossSeedInventoryState.Init();
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
                    _recycleScript.Unload();
                    Destroy(_recycleScript.gameObject);
                    _recycleScript = null;
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

            if (_recycleScript == null && GameMain.isRunning && LogisticsNetwork.IsInitted && GameMain.mainPlayer != null)
            {
                _recycleScript = gameObject.AddComponent<RecycleWindow>();
            }
        }

        public void Export(BinaryWriter w)
        {
            w.Write(VERSION);
            PersonalLogisticManager.Export(w);
            ShippingManager.Export(w);
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
        }

        public void IntoOtherSave()
        {
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
                v => { RequestWindow.Visible = !RequestWindow.Visible; });
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
            CrossSeedInventoryState.Save();
            CrossSeedInventoryState.Reset();
            InventoryManager.Reset();
            RequestWindow.Reset();
            ShippingManager.Reset();
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
    }
}