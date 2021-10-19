using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PersonalLogistics.UI;
using PersonalLogistics.Util;
using UnityEngine;

namespace PersonalLogistics
{
    public class TimeScript : MonoBehaviour
    {
        private StringBuilder timeText;
        private string positionText;
        private GUIStyle fontSize;
        private GUIStyle style;
        private bool loggedException = false;
        private static int yOffset = 0;
        private static int xOffset = 0;
        private Vector3 _positionWhenLastOrderGiven = Vector3.zero;
        private OrderNode _lastOrder;
        private DateTime _lastOrderCreatedAt;

        void Awake()
        {
            StartCoroutine(Loop());
            fontSize = new GUIStyle(GUI.skin.GetStyle("label"))
            {
                fontSize = UI.UiScaler.ScaleToDefault(12, false)
            };
            style = new GUIStyle
            {
                normal = new GUIStyleState { textColor = Color.white },
                wordWrap = false,
            };
        }

        public void OnGUI()
        {
            if ((timeText == null || timeText.Length == 0) && string.IsNullOrEmpty(positionText))
            {
                return;
            }

            var uiGame = UIRoot.instance.uiGame;
            if (uiGame == null)
            {
                return;
            }

            if (uiGame.starmap.active || uiGame.dysonmap.active || uiGame.globemap.active || uiGame.escMenu.active || uiGame.techTree.active)
            {
                return;
            }

            var text = (timeText == null ? "" : timeText.ToString()) + (positionText ?? "");
            var minWidth = UiScaler.ScaleToDefault(600, true);
            var height = style.CalcHeight(new GUIContent(text), minWidth) + 10;

            var rect = GUILayoutUtility.GetRect(minWidth, height * 1.25f);

            if (yOffset == 0)
            {
                DetermineYOffset();
            }

            DetermineXOffset();
            GUI.Label(new Rect(xOffset, yOffset, rect.width, rect.height), text, fontSize);
        }

        private void DetermineYOffset()
        {
            yOffset = (int)(Screen.height / 10f);
        }

        private void DetermineXOffset()
        {
            var manualResearch = GameObject.Find("UI Root/Overlay Canvas/In Game/Windows/Mini Lab Panel");
            if (manualResearch != null && manualResearch.activeSelf)
            {
                xOffset = UiScaler.ScaleToDefault(250);
            }
            else
                xOffset = UiScaler.ScaleToDefault(150);
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
                        timeText.Append($"{loadState.itemName} arriving in {loadState.secondsRemaining + 5} seconds\r\n");
                    }
                }
                else
                {
                    timeText = null;
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

        IEnumerator Loop()
        {
            while (true)
            {
                yield return new WaitForSeconds(2);
                if (PluginConfig.showIncomingItemProgress.Value)
                    UpdateIncomingItems();
                else
                    timeText = null;

                yield return new WaitForSeconds(2);
                if (PluginConfig.followBluePrint.Value)
                {
                    FollowBluePrint();
                }

                if (PluginConfig.showNearestBuildGhostIndicator.Value)
                    AddGhostStatus();
                else
                    positionText = null;
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
            Vector3 closest = Vector3.zero;
            var closestDist = float.MaxValue;
            string closestItemName = "";
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
                }
            }

            if (closestDist < float.MaxValue && OutOfBuildRange(closestDist))
            {
                var coords = PositionToLatLonString(closest);
                positionText = $"Nearest ghost at {coords}, {closestItemName} (total: {ctr})\r\n";
            }
            else
            {
                // positionText = "Nothing inbound";
                positionText = null;
            }
        }

        private void FollowBluePrint()
        {
            if (GameMain.localPlanet == null || GameMain.localPlanet.factory == null)
            {
                positionText = null;
                return;
            }

            // if (GameMain.mainPlayer.controller.movementStateInFrame != EMovementState.Fly)
            // {
            //     GameMain.mainPlayer.controller.movementStateInFrame = EMovementState.Fly;
            //     GameMain.mainPlayer.controller.actionFly.targetAltitude = 20f;
            // }

            var northPole = GameMain.localPlanet.realRadius * Vector3.up;
            var intPoints = new HashSet<Vector3Int>();
            var points = new List<Vector3>();
            foreach (var prebuildData in GameMain.localPlanet.factory.prebuildPool)
            {
                if (prebuildData.id < 1)
                {
                    continue;
                }

                var intPos = new Vector3Int((int)prebuildData.pos.x, (int)prebuildData.pos.y, (int)prebuildData.pos.z);
                if (intPoints.Contains(intPos))
                {
                    continue;
                }

                intPoints.Add(intPos);

                points.Add(prebuildData.pos);
            }

            points.Sort((p1, p2) =>
            {
                var p1Distance = Vector3.Distance(northPole, p1);
                var p2Distance = Vector3.Distance(northPole, p2);

                return p1Distance.CompareTo(p2Distance);
            });
            if (points.Count == 0)
            {
                return;
            }

            if (GameMain.mainPlayer.orders.orderCount == 0)
            {
                // if (Vector3.Distance(_positionWhenLastOrderGiven, GameMain.mainPlayer.position) < 5)
                // {
                //     // maybe stuck, try and go north a bit
                //     Log.LogAndPopupMessage($"trying to get unstuck");
                //
                //     GameMain.mainPlayer.Order(OrderNode.MoveTo(GameMain.mainPlayer.position + Vector3.up), true);
                // }
                // else
                if (_lastOrder == null || _lastOrder.targetReached || (_lastOrderCreatedAt != null && (DateTime.Now - _lastOrderCreatedAt).TotalSeconds > 5)) 
                {
                    // if (GameMain.mainPlayer.mecha.idleDroneCount == 0)
                    // {
                    //     var taskedDroneCount = GameMain.mainPlayer.mecha.droneCount - GameMain.mainPlayer.mecha.idleDroneCount;
                    //     Log.LogAndPopupMessage($"Waiting for {taskedDroneCount} to return");
                    //     return;
                    // }
                    _lastOrder = OrderNode.MoveTo(points[0]);
                    _lastOrderCreatedAt = DateTime.Now;
                    _positionWhenLastOrderGiven = GameMain.mainPlayer.position;
                    GameMain.mainPlayer.Order(_lastOrder, true);
                    Log.Debug($"initiated order to move to {_lastOrder.target}");
                }
                else
                {
                    Log.Debug($"last order {_lastOrder?.targetReached} {_lastOrder?.target}");
                }
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
            Maths.GetLatitudeLongitude(position, out int latd, out int latf, out int logd, out int logf, out bool north, out bool south, out bool west,
                out bool east);
            string latDir = north ? "N" : "S";
            string lonDir = east ? "E" : "W";
            var latCoord = $"{latd}° {latf}' {latDir}";

            string lonCoord = $"{logd}° {logf}' {lonDir}";
            return $"{latCoord}, {lonCoord}";
        }

        public static void ClearOffset()
        {
            yOffset = 0;
        }

        public void Unload()
        {
        }
    }
}