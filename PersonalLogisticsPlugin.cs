using System;
using System.Collections.Generic;
using BepInEx;
using HarmonyLib;
using UnityEngine;
using static NetworkManager.Log;
using Debug = UnityEngine.Debug;

namespace NetworkManager
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInProcess("DSPGAME.exe")]
    public class NetworkManagerPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "semarware.dysonsphereprogram.NetworkManager";
        public const string PluginName = "NetworkManager";
        public const string PluginVersion = "1.0.0";

        private Harmony _harmony;

        private UIElements _ui;
        private static NetworkManagerPlugin instance;

        // Awake is called once when both the game and the plugin are loaded
        private void Awake()
        {
            logger = Logger;
            instance = this;
            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll(typeof(NetworkManagerPlugin));
            _harmony.PatchAll(typeof(StationSelectionWindow));
            PluginConfig.InitConfig(Config);
            Debug.Log("NetworkManager Plugin Loaded");
        }

        private bool _menuOpen = false;
        private List<GameObject> _objectsToDestroy = new List<GameObject>();

        private void Update()
        {
            if (!LogisticsNetwork.IsInitted && GameMain.mainPlayer != null && GameMain.mainPlayer.factory != null)
            {
                LogisticsNetwork.Start();
            }

            if (VFInput.control && Input.GetKeyDown(KeyCode.F3))
            {
                var parent = GameObject.Find("UI Root/Overlay Canvas/In Game/");
                var componentsInParent = parent.GetComponentsInChildren<UIStationWindow>();
                logger.LogDebug($"found {componentsInParent.Length} windows to close");
                foreach (var stationWindow in componentsInParent)
                {
                    Destroy(stationWindow.gameObject);
                }

                UIRoot.ClearFatalError();
                return;
            }

            if (Input.GetKeyDown(KeyCode.F3))
            {
                logger.LogWarning($"UIRoot.instance.uiGame.buildMenu.currentCategory == {UIRoot.instance.uiGame.buildMenu.currentCategory}");
                if (UIRoot.instance.uiGame.buildMenu.currentCategory != 0)
                {
                    return;
                }
                if (!_menuOpen)
                {
                    try
                    {
                        if (_ui == null)
                        {
                            instance.InitUi();
                        }

                        // _ui.OpenWindow();
                        StationSelectionWindow.visible = true;
                    }
                    catch (Exception e)
                    {
                        logger.LogWarning($"failed to open window {e}");
                        logger.LogWarning(e.StackTrace);
                    }
                }
            }
        }


        private void OnDestroy()
        {
            if (_ui != null)
            {
                _ui.Unload();
            }

            StationSelectionWindow.NeedReinit = true;
            LogisticsNetwork.Stop();
            foreach (var gameObj in _objectsToDestroy)
            {
                Destroy(gameObj);
            }

            _objectsToDestroy.Clear();
            _harmony.UnpatchSelf();
        }

        public void OnGUI()
        {
            if (StationSelectionWindow.visible)
            {
                StationSelectionWindow.OnGUI();
            }
        }

        private void InitUi()
        {
            GameObject parentGo = GameObject.Find("UI Root/Overlay Canvas/In Game");
            var containerRect = parentGo.GetComponent<RectTransform>();
            _ui = containerRect.gameObject.AddComponent<UIElements>();
            UIElements.logger = logger;
            if (containerRect == null)
            {
                return;
            }
        }
    }
}