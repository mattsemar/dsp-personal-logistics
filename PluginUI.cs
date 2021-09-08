using System;
using System.Collections.Generic;
using BepInEx.Logging;
using JetBrains.Annotations;
using UnityEngine;

namespace NetworkManager
{
    public class UIElements : MonoBehaviour
    {
        public static ManualLogSource logger;
        private static List<GameObject> gameObjectsToDestroy = new List<GameObject>();

        private bool _techUnlocked;
        private bool _menuOpen;
        public UIStationWindow uiStationWindow;
        public static bool CloseStationRequested;

        public bool TechUnlockedState
        {
            get => _techUnlocked;
            set
            {
                _techUnlocked = value;
                if (!_techUnlocked)
                {
                    // mainActionButton.button.interactable = false;
                    // mainActionButton.tips.tipText = "Research Universe Exploration 3 to Unlock";
                }
            }
        }

        public void Update()
        {
            if (CloseStationRequested)
            {
                CloseStationRequested = false;
                if (uiStationWindow != null)
                {
                    uiStationWindow._Close();
                }
            }
            else
            {
                try
                {
                    OpenWindow();
                }
                catch (Exception e)
                {
                    logger.LogWarning($"caught exception while trying to open window {e.Message} {e.StackTrace}");
                }
            }

            if (uiStationWindow != null && uiStationWindow.active)
            {
                uiStationWindow._OnUpdate();
            }
        }


        public void Unload()
        {
            try
            {
                while (gameObjectsToDestroy.Count > 0)
                {
                    Destroy(gameObjectsToDestroy[0]);
                    gameObjectsToDestroy.RemoveAt(0);
                }
            }
            catch (Exception e)
            {
                logger.LogWarning($"failed to do unload {e.Message}");
                logger.LogWarning(e.StackTrace);
            }
        }

        public void Show()
        {
        }

        public void Hide()
        {
        }

        public void OpenWindow()
        {
            if (StationSelectionWindow.StationToOpen == null)
            {
                return;
            }

            if (UIRoot.instance.uiGame.stationWindow == null)
            {
                logger.LogDebug("global ui stationWindow is null");
                return;
                // UIRoot.instance.uiGame.OpenStationWindow();
                // if (UIRoot.instance.uiGame.stationWindow == null)
                // {
                //     Console.WriteLine($"station window not instantiated");
                //     return;
                // }
                // UIRoot.instance.uiGame.stationWindow._OnClose();
            }
            var stationToOpen = StationSelectionWindow.StationToOpen;
            StationSelectionWindow.StationToOpen = null;
            if (uiStationWindow != null)
            {
                try
                {
                    uiStationWindow._Close();
                }
                catch (Exception e)
                {
                    logger.LogWarning($"exception while closing previous window {e.Message}\n{e.StackTrace}");
                }
            }

       
            uiStationWindow = Instantiate(UIRoot.instance.uiGame.stationWindow, UIRoot.instance.uiGame.gameObject.transform, false);
            if (uiStationWindow == null || uiStationWindow.gameObject == null)
            {
                logger.LogWarning($"station window not returned {uiStationWindow} {uiStationWindow.gameObject}");
            }
            uiStationWindow.gameObject.SetActive(true);

            var planetById = GameMain.galaxy.PlanetById(stationToOpen.PlanetId);
            if (planetById == null)
                logger.LogWarning($"planet not returned {stationToOpen.PlanetId}");
            var planetFactory = planetById.factory;
            if (planetFactory == null)
            {
                logger.LogWarning($"factory is null");
            }
            uiStationWindow.factory = planetFactory;

            if (uiStationWindow.factory == null)
            {
                logger.LogDebug("factory is null, guess it is not loaded");
                return;
            }

            uiStationWindow.transport = planetFactory.transport;
            uiStationWindow.stationId = stationToOpen.stationId;
            // if (uiStationWindow.storageUIs != null)
            // {
            //     foreach (var uiStationStorage in uiStationWindow.storageUIs)
            //     {
            //         uiStationStorage.gameObject.SetActive(true);
            //     }
            // }
            uiStationWindow.active = true;

            uiStationWindow._OnCreate();
            uiStationWindow._OnInit();
            uiStationWindow._OnOpen();
            uiStationWindow.factory = planetFactory;
            if (uiStationWindow.factory == null)
            {
                logger.LogDebug("factory is null, guess it is not loaded");
                return;
            }

            uiStationWindow.transport = planetFactory.transport;
            uiStationWindow.factory = planetFactory;
            uiStationWindow.powerSystem = planetFactory.powerSystem;
            uiStationWindow.stationId = stationToOpen.stationId;
            uiStationWindow.OnStationIdChange();
            var stationComponent = GetStationComp(stationToOpen);
            
            if (uiStationWindow.storageUIs != null)
                foreach (var storageUI in uiStationWindow.storageUIs)
                {
                    storageUI.station = stationComponent;
                    storageUI.RefreshValues();
                }
            uiStationWindow._OnUpdate();
            if (UIRoot.instance.uiGame != null && !UIRoot.instance.uiGame.inventory.active)
            {
                UIRoot.instance.uiGame.OpenPlayerInventory();
            }
                
        }

        [CanBeNull]
        private static StationComponent GetStationComp(StationInfo stationInfo)
        {
            try
            {
                var planetById = GameMain.galaxy.PlanetById(stationInfo.PlanetId);
                var planetFactory = planetById?.factory;
                if (planetById == null || planetFactory == null)
                {
                    
                    Log.logger.LogWarning($"either planet or factory is null {planetById} {planetFactory} {stationInfo.PlanetId}");
                    return null;
                }

                var stationComponent = planetById.factory.transport.stationPool[stationInfo.stationId];
                if (stationComponent.planetId == 0)
                {
                    Log.logger.LogWarning($"planet id for component is 0. should be {stationInfo.PlanetId}");
                }
                return stationComponent;
            }
            catch (Exception e)
            {
                Log.logger.LogWarning($"failed to get station comp {e.Message}");
                Log.logger.LogWarning(e.StackTrace);
            }

            return null;
        }
        
    }

}