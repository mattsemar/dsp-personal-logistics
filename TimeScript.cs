using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace PersonalLogistics
{
    public class TimeScript : MonoBehaviour
    {
        private StringBuilder timeText;
        private string positionText;
        private GUIStyle fontSize;
        private GUIStyle style;
        private Text textComponent;
        private Transform waypointArrow;
        private Transform currentWaypoint;
        private bool loggedException = false;
        private static int yOffset = 0;

        void Awake()
        {
            StartCoroutine(Loop());
            fontSize = new GUIStyle(GUI.skin.GetStyle("label"))
            {
                fontSize = 16
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

            if (uiGame.starmap.active || uiGame.dysonmap.active || uiGame.globemap.active || uiGame.escMenu.active)
            {
                return;
            }

            var text = (timeText == null ? "" : timeText.ToString()) + (positionText ?? "");
            var height = style.CalcHeight(new GUIContent(text), 600) + 10;

            var rect = GUILayoutUtility.GetRect(600, height * 1.25f);

            if (yOffset == 0)
            {
                DetermineYOffset();
            }

            GUI.Label(new Rect(100, yOffset, rect.width, rect.height), text, fontSize);
        }

        private void DetermineYOffset()
        {
            yOffset = (int)(Screen.height / 10f);
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
                }
            }

            if (closestDist < float.MaxValue && OutOfBuildRange(closestDist))
            {
                var coords = PositionToLatLonString(closest);
                positionText = $"Nearest ghost {coords} (total: {ctr})\r\n";
            }
            else
            {
                // positionText = "Nothing inbound";
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
            Maths.GetLatitudeLongitude(position, out int latd, out int latf, out int logd, out int logf, out bool north, out bool south, out bool west,
                out bool east);
            string latDir = north ? "N" : "S";
            string lonDir = east ? "E" : "E";
            var latCoord = $"{latd}° {latf}' {latDir}";

            string lonCoord = $"{logd}° {logf}' {lonDir}";
            return $"{latCoord}, {lonCoord}";
        }

        public void CreateArrow()
        {
            if (LoadFromFile.InitBundle())
            {
                var prefab = LoadFromFile.LoadPrefab("Assets/TurnTheGameOn/Arrow WayPointer/Resources/Waypoint Arrow.prefab");
                GameObject instance = Instantiate(prefab, GameMain.mainPlayer.transform.parent, false);
                var component = instance.GetComponent<Transform>();
                Console.WriteLine($"transform {component.position} {component.rotation}");
                instance.name = "Waypoint Arrow";
            }
        }

        public static void ClearOffset()
        {
            yOffset = 0;
        }
    }
}