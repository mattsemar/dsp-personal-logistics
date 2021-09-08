using System;

using UnityEngine;

namespace NetworkManager
{
    public class LogisticsWindow 
    {
        public static bool isShow;

        private static Rect windowRect = new Rect(300f, 250f, 350f, 400f);
        private static float vScrollbarValue;
        private static Vector2 scrollViewVector = Vector2.zero;


        public static void OnOpen()
        {
            isShow = true;
        }
 
        public static void OnClose()
        {
            isShow = false;
        }

        public static void OnGUI()
        {
            // vScrollbarValue = GUI.VerticalScrollbar (new Rect (25, 25, 100, 30), vScrollbarValue, 1.0f, 10.0f, 0.0f);
            windowRect = GUILayout.Window(1297890673, windowRect, NormalWindowFun, "Logistics network");
            EatInputInRect(windowRect);
        }
        
        static void NormalWindowFun(int id)
        {
            bool tmpBool;
            int tmpInt;
            string tmpString;

            GUILayout.BeginArea(new Rect(windowRect.width - 27f, 1f, 25f, 30f));
            if (GUILayout.Button("x"))
            {
                isShow = false;
                OnClose();
                return;
            }
            GUILayout.EndArea();

            GUILayout.BeginVertical(GUI.skin.box);
            scrollViewVector = GUI.BeginScrollView (new Rect (0f, 30f, windowRect.width - 5, 300),
                scrollViewVector, new Rect (0, 30f, windowRect.width - 5, 400));

            GUILayout.BeginHorizontal();
            GUILayout.Label($"Network initialized {LogisticsNetwork.IsInitted}");
            GUILayout.EndHorizontal();
            if (LogisticsNetwork.IsInitted && LogisticsNetwork.stations.Count > 0)
            {
                // var stationsWithFoundation = LogisticsNetwork.stations.FindAll(x => x.HasItem(PlatformSystem.REFORM_ID));
                // int countDown = 25;
                
                // foreach (var stationInfo in stationsWithFoundation)
                // {
                    GUILayout.BeginHorizontal();
                    var logisticsNetworkSummaryItem = LogisticsNetwork.GetSummaryItem(PlatformSystem.REFORM_ID);
                    GUILayout.Label($"Foundation available {logisticsNetworkSummaryItem.Count}");
                    GUILayout.Label($"Stations {logisticsNetworkSummaryItem.StationCount}");
                    GUILayout.Label($"Planets {logisticsNetworkSummaryItem.Planets.Count}");
                    
                    GUILayout.EndHorizontal();
                    var logisticsNetworkSummaryItems = LogisticsNetwork.GetSummary();
                    foreach (var item in logisticsNetworkSummaryItems)
                    {
                        GUILayout.BeginHorizontal();
                        GUILayout.Label($"{item.ItemName.Translate()} {item.Count}");
                        GUILayout.Label($"Stations {item.StationCount}");
                        GUILayout.Label($"Planets {item.Planets.Count}");
                    
                        GUILayout.EndHorizontal();
                    }
                    //     if (countDown-- <= 0)
                //         break;
                // }
            }

            GUI.EndScrollView();
            GUILayout.EndVertical();
            GUI.DragWindow();
        }
        public static void EatInputInRect(Rect eatRect)
        {
            if (!(Input.GetMouseButton(0) || Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(2))) //Eat only when left-click
                return;
            if (eatRect.Contains(new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y)))
                Input.ResetInputAxes();
        }
    }
}