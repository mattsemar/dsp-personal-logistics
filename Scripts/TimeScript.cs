﻿using System;
using System.Collections;
using System.Text;
using PersonalLogistics.Model;
using PersonalLogistics.PlayerInventory;
using PersonalLogistics.UI;
using PersonalLogistics.Util;
using UnityEngine;
using UnityEngine.UI;

namespace PersonalLogistics.Scripts
{
    public class TimeScript : MonoBehaviour
    {
        private static int yOffset = 0;
        private static int xOffset = 0;
        private RectTransform _arrivalTimeRT;
        private Text _incomingText;
        private Rect _rect;
        private int _savedHeight;
        private int _savedWidth;
        private Texture2D back;
        private GUIStyle fontSize;
        private bool loggedException;
        private string positionText;
        private GUIStyle style;
        private StringBuilder timeText;
        private GameObject txtGO;


        private void Awake()
        {
            InitText();
            StartCoroutine(Loop());
        }

        private void Update()
        {
            if ((timeText == null || timeText.Length == 0) && string.IsNullOrEmpty(positionText))
            {
                _incomingText.text = "";
                return;
            }

            var uiGame = UIRoot.instance.uiGame;
            if (uiGame == null)
            {
                _incomingText.text = "";
                return;
            }

            if (uiGame.starmap.active || uiGame.dysonmap.active || uiGame.globemap.active || uiGame.escMenu.active || uiGame.techTree.active)
            {
                _incomingText.text = "";
                return;
            }

            if (Time.frameCount % 105 == 0)
            {
                AdjustLocation();
            }

            var text = (timeText == null ? "" : timeText.ToString()) + (positionText ?? "");
            _incomingText.text = text;
        }

        private void UpdateIncomingItems()
        {
            try
            {
                var itemLoadStates = ItemLoadState.GetLoadState();
                if (itemLoadStates == null)
                {
                    return;
                }

                if (itemLoadStates.Count > 0)
                {
                    timeText = new StringBuilder();
                    foreach (var loadState in itemLoadStates)
                    {
                        var etaStr = TimeUtil.FormatEta(loadState.secondsRemaining);
                        timeText.Append($"{loadState.itemName} (x{loadState.count}) ETA {etaStr}\r\n");
                    }
                }
                else
                {
                    timeText = null;
                    if (PluginConfig.timeScriptPositionTestEnabled.Value)
                    {
                        timeText = new StringBuilder("Iron ingot (x15) ETA 06:00\r\nIron plate (x200) ETA 21:00");
                    }
                }
            }
            catch (Exception e)
            {
                if (!loggedException)
                {
                    loggedException = true;
                    Log.Warn($"failure while updating incoming items {e.Message} {e.StackTrace}");
                }
            }
        }

        private IEnumerator Loop()
        {
            while (true)
            {
                yield return new WaitForSeconds(2);
                if (!PluginConfig.inventoryManagementPaused.Value && PluginConfig.showIncomingItemProgress.Value)
                {
                    UpdateIncomingItems();
                }
                else
                {
                    timeText = null;
                }

                yield return new WaitForSeconds(2);
                if (!PluginConfig.inventoryManagementPaused.Value && PluginConfig.showNearestBuildGhostIndicator.Value)
                {
                    AddGhostStatus();
                }
                else
                {
                    positionText = null;
                }
            }
        }

        private void AddGhostStatus()
        {
            if (GameMain.localPlanet == null || GameMain.localPlanet.factory == null)
            {
                positionText = null;
                return;
            }

            var ctr = 0;
            var playerPosition = GameMain.mainPlayer.position;
            var closest = Vector3.zero;
            var closestDist = float.MaxValue;
            var closestItemName = "";
            var closestItemId = 0;
            foreach (var prebuildData in GameMain.localPlanet.factory.prebuildPool)
            {
                if (prebuildData.id < 1)
                {
                    continue;
                }

                ctr++;
                var distance = Vector3.Distance(prebuildData.pos, playerPosition);
                if (distance < closestDist && OutOfBuildRange(distance))
                {
                    closest = prebuildData.pos;
                    closestDist = distance;
                    closestItemName = ItemUtil.GetItemName(prebuildData.protoId);
                    closestItemId = prebuildData.protoId;
                }
            }

            if (closestDist < float.MaxValue && OutOfBuildRange(closestDist))
            {
                var coords = PositionToLatLonString(closest);
                var parensPart = $"(total: {ctr})";
                if (closestItemId > 0 && !InventoryManager.IsItemInInventoryOrInbound(closestItemId))
                {
                    parensPart = "(Not available)";
                }

                positionText = $"Nearest ghost at {coords}, {closestItemName} {parensPart}\r\n";
            }
            else
            {
                positionText = null;
            }
        }

        private bool OutOfBuildRange(float closestDist)
        {
            var localPlanet = GameMain.localPlanet;
            var mechaBuildArea = GameMain.mainPlayer?.mecha?.buildArea;
            if (localPlanet == null || localPlanet.type == EPlanetType.Gas)
            {
                return false;
            }

            return closestDist > mechaBuildArea;
        }

        private static string PositionToLatLonString(Vector3 position)
        {
            Maths.GetLatitudeLongitude(position, out var latd, out var latf, out var logd, out var logf, out var north, out _, out _,
                out var east);
            var latDir = north ? "N" : "S";
            var lonDir = east ? "E" : "W";
            var latCoord = $"{latd}° {latf}' {latDir}";

            var lonCoord = $"{logd}° {logf}' {lonDir}";
            return $"{latCoord}, {lonCoord}";
        }

        public void Unload()
        {
            try
            {
                if (_incomingText != null)
                {
                    Destroy(_incomingText.gameObject);
                }
            }
            catch (Exception ignored)
            {
                // ignored
            }
        }

        private void InitText()
        {
            txtGO = new GameObject("arrivalTimeText");
            _arrivalTimeRT = txtGO.AddComponent<RectTransform>();
            var inGameGo = GameObject.Find("UI Root/Overlay Canvas/In Game");
            _arrivalTimeRT.anchorMax = new Vector2(0, 0.5f);
            _arrivalTimeRT.anchorMin = new Vector2(0, 0.5f);
            _arrivalTimeRT.sizeDelta = new Vector2(100, 20);
            _arrivalTimeRT.pivot = new Vector2(0, 0.5f);
            _incomingText = _arrivalTimeRT.gameObject.AddComponent<Text>();
            _incomingText.text = "Hello operator";
            _incomingText.fontStyle = FontStyle.Normal;
            _incomingText.fontSize = 20;
            _incomingText.verticalOverflow = VerticalWrapMode.Overflow;
            _incomingText.horizontalOverflow = HorizontalWrapMode.Overflow;
            _incomingText.color = new Color(1f, 1f, 1f, 1f);

            var fnt = Resources.Load<Font>("ui/fonts/SAIRASB");
            if (fnt != null)
            {
                _incomingText.font = fnt;
            }

            txtGO.transform.SetParent(inGameGo.transform, false);
            AdjustLocation();
        }

        private void AdjustLocation()
        {
            if (_savedHeight == DSPGame.globalOption.uiLayoutHeight && _savedWidth == Screen.width)
            {
                return;
            }

            if (_arrivalTimeRT == null)
            {
                return;
            }

            _savedHeight = DSPGame.globalOption.uiLayoutHeight;
            _savedWidth = Screen.width;
            _arrivalTimeRT.anchoredPosition = new Vector2(UiScaler.ScaleToDefault(25), _savedHeight / 4f);

            _incomingText.fontSize = UiScaler.ScaleToDefault(12, false);
        }
    }
}