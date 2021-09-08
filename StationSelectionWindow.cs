using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using JetBrains.Annotations;
using UnityEngine;

namespace NetworkManager
{
    internal enum Mode
    {
        None, // not yet selected
        ByPlanet,
        ByItem
    }

    internal class Pager<T>
    {
        public int PageNum = 0;

        public int PageSize = 20;

        // public int Count = 0;
        public List<T> _items;

        public Pager(List<T> items, int pageSize = 10)
        {
            PageSize = pageSize;
            _items = new List<T>(items);
        }

        public int Count => _items.Count;

        public void Reset()
        {
            PageNum = 0;
        }

        public void Next()
        {
            PageNum++;
        }

        public bool IsFirst()
        {
            return PageNum == 0;
        }

        public (int startIndex, int endIndex) GetIndexes()
        {
            var beginNdx = PageNum * PageSize;
            return (PageNum * PageSize, Math.Min(beginNdx + PageSize, _items.Count));
        }

        public List<T> GetPage()
        {
            var (startIndex, endIndex) = GetIndexes();
            return _items.GetRange(startIndex, endIndex - startIndex);
        }

        public bool HasNext()
        {
            return _items.Count > GetIndexes().endIndex + 1;
        }

        public bool IsEmpty()
        {
            return _items.Count == 0;
        }

        public void Previous()
        {
            PageNum--;
        }
    }

    public class StationSelectionWindow
    {
        public static bool visible;
        private static bool _requestHide;
        private static Rect _windowRect = new Rect(300f, 250f, 500f, 600f);

        public static bool NeedReinit;

        private static Texture2D _tooltipBg;

        private static int _loggedMessageCount = 0;
        private static string _savedGUISkin;
        private static GUISkin _savedGUISkinObj;
        private static Color _savedColor;
        private static Color _savedBackgroundColor;
        private static Color _savedContentColor;
        private static GUISkin _mySkin;
        private static int _chosenPlanet = -1;
        private static Mode _mode = Mode.None;
        private static Pager<StationInfo> _pager;
        private static PlanetPickerWindow _planetPickerWindow;
        public static StationInfo StationToOpen;

        public static Texture2D Background
        {
            get
            {
                Init();
                return _tooltipBg;
            }
        }

        public static void OnClose()
        {
            visible = false;
            _requestHide = false;
            _pager = null;
            _mode = Mode.None;
            _planetPickerWindow?.OnClose();

            RestoreGuiSkinOptions();
        }

        public static void OnGUI()
        {
            if (Input.GetKeyDown(KeyCode.Escape) || _requestHide)
            {
                OnClose();
                return;
            }

            if (_planetPickerWindow != null && _planetPickerWindow.visible)
            {
                _planetPickerWindow.OnGUI();
            }

            if (_planetPickerWindow != null && _planetPickerWindow.PlanetPicked) // && Event.current.type==EventType.Repaint)
            {
                var chosenPlanet = _planetPickerWindow.ChosenPlanet;
                _planetPickerWindow.PlanetPicked = false;
                _planetPickerWindow.visible = false;
                var list = LogisticsNetwork.byPlanet[chosenPlanet.PlanetId];
                _pager = new Pager<StationInfo>(list);
                _mode = Mode.ByPlanet;
            }

            Init();

            _windowRect = GUILayout.Window(1297898555, _windowRect, WindowFnWrapper, "NetworkManager options");
        }

        public static void SaveCurrentGuiOptions()
        {
            _savedBackgroundColor = GUI.backgroundColor;
            _savedContentColor = GUI.contentColor;
            _savedColor = GUI.color;
            GUI.backgroundColor = Color.white;
            GUI.contentColor = Color.white;
            GUI.color = Color.white;


            if (_mySkin == null || NeedReinit)
            {
                _savedGUISkin = JsonUtility.ToJson(GUI.skin);
                _savedGUISkinObj = GUI.skin;
                _mySkin = ScriptableObject.CreateInstance<GUISkin>();
                JsonUtility.FromJsonOverwrite(_savedGUISkin, _mySkin);
                GUI.skin = _mySkin;
                GUI.skin.button.normal.textColor = Color.white;
                GUI.skin.textArea.normal.textColor = Color.white;
                GUI.skin.textField.normal.textColor = Color.white;
                GUI.skin.toggle.normal.textColor = Color.white;
                GUI.skin.toggle.onNormal.textColor = Color.white;
                GUI.skin.button.normal.textColor = Color.white;
                GUI.skin.button.onNormal.textColor = Color.white;
                GUI.skin.button.onActive.textColor = Color.white;
                GUI.skin.button.active.textColor = Color.white;
                GUI.skin.label.hover.textColor = Color.white;
                GUI.skin.label.onNormal.textColor = Color.white;
                GUI.skin.label.normal.textColor = Color.white;
            }
            else
            {
                _savedGUISkinObj = GUI.skin;
                GUI.skin = _mySkin;
            }
        }

        public static void RestoreGuiSkinOptions()
        {
            GUI.skin = _savedGUISkinObj;
            GUI.backgroundColor = _savedBackgroundColor;
            GUI.contentColor = _savedContentColor;
            GUI.color = _savedColor;
        }

        private static void Init()
        {
            if (_tooltipBg == null && !NeedReinit)
            {
                return;
            }

            var background = new Texture2D(1, 1, TextureFormat.RGB24, false);
            background.SetPixel(0, 0, Color.white);
            background.Apply();
            _tooltipBg = background;
            InitWindowRect();
            NeedReinit = false;
        }

        private static void WindowFnWrapper(int id)
        {
            SaveCurrentGuiOptions();
            WindowFn();
            GUI.DragWindow();
            RestoreGuiSkinOptions();
        }

        private static void WindowFn()
        {
            GUILayout.BeginArea(new Rect(_windowRect.width - 25f, 0f, 25f, 30f));
            if (GUILayout.Button("X"))
            {
                OnClose();
                return;
            }

            GUILayout.EndArea();
            GUILayout.BeginVertical(GUI.skin.box);
            {
                if (_mode == Mode.None && !(LogisticsNetwork.IsInitted && LogisticsNetwork.IsFirstLoadComplete))
                {
                    DrawCenteredLabel("Loading...");
                    GUILayout.EndVertical();
                    GUI.DragWindow();
                    return;
                }

                if (_mode == Mode.None)
                    DrawModeSelector();

                // else
                // {
                if (_mode != Mode.None && _pager != null)
                    DrawCenteredLabel($"Stations by {_mode}");
                else if (_mode != Mode.None)
                {
                    if (_mode == Mode.ByItem)
                    {
                        DrawByItemButton();
                    }
                    else
                    {
                        DrawByPlanetButton();
                    }
                }
                // }

                if (_mode != Mode.None && (_pager == null || (_pager.IsFirst() && _pager.IsEmpty())))
                {
                    GUILayout.Label($"No stations found");
                }
                else if (_pager != null)
                {
                    DrawCountLabel();
                    DrawPreviousButton();
                    var stationInfos = _pager.GetPage();
                    GUILayout.BeginVertical();
                    foreach (var stationInfo in stationInfos)
                    {
                        GUILayout.BeginHorizontal();
                        GUILayout.Label(new GUIContent($@"{stationInfo.PlanetName} {stationInfo.stationId} {stationInfo.ProductNameList()}",
                            JsonUtility.ToJson(stationInfo)));
                        var pressed = GUILayout.Button("Manage");
                        if (pressed)
                        {
                            StationToOpen = stationInfo;
                        }

                        GUILayout.EndHorizontal();
                    }

                    GUILayout.EndVertical();
                    DrawNextButton();
                }
            }
            GUILayout.EndVertical();
            if (GUI.tooltip != null)
            {
                GUI.skin = null;
                var style = new GUIStyle
                {
                    normal = new GUIStyleState { textColor = Color.white },
                    wordWrap = true,
                    alignment = TextAnchor.MiddleCenter,
                    stretchHeight = true,
                    stretchWidth = true
                };

                var height = style.CalcHeight(new GUIContent(GUI.tooltip), _windowRect.width) + 10;
                var y = (int)(_windowRect.height - height * 1.25);
                GUI.Box(new Rect(0, y, _windowRect.width, height * 1.25f), GUI.tooltip, style);
            }
        }

       private static void DrawCountLabel()
        {
            GUILayout.BeginHorizontal();
            var (startIndex, endIndex) = _pager.GetIndexes();
            GUILayout.Label($"Items {startIndex}-{endIndex} of {_pager.Count}");

            GUILayout.EndHorizontal();
        }

        private static void DrawNextButton()
        {
            if (!_pager.HasNext())
                return;
            GUILayout.BeginHorizontal();
            var buttonPressed = GUILayout.Button("Next");
            if (buttonPressed)
            {
                Console.WriteLine($"paging to next page {_pager.GetIndexes()}");
                _pager.Next();
                Console.WriteLine($"next page {_pager.GetIndexes()}");
            }

            GUILayout.EndHorizontal();
        }

        private static void DrawPreviousButton()
        {
            if (_pager.IsFirst())
                return;
            GUILayout.BeginHorizontal();
            var buttonPressed = GUILayout.Button("Previous");
            if (buttonPressed)
            {
                _pager.Previous();
            }

            GUILayout.EndHorizontal();
        }

        private static void DrawModeSelector()
        {
            var guiContents = new[]
            {
                new GUIContent("Clear", "Clear filter"),
                new GUIContent("By Planet", "Filter stations by planet"),
                new GUIContent("By Item", "Filter stations by item"),
            };
            GUILayout.BeginVertical("Box");

            int currentlySelected = 0;
            if (_mode == Mode.ByItem)
            {
                currentlySelected = 2;
                _mode = Mode.ByItem;
            }
            else if (_mode == Mode.ByPlanet)
            {
                currentlySelected = 1;
                _mode = Mode.ByPlanet;
            }

            var newIndex = GUILayout.Toolbar(currentlySelected, guiContents);

            if (newIndex != currentlySelected)
            {
                _mode = (Mode)Enum.GetValues(typeof(Mode)).GetValue(newIndex);
                if (_mode == Mode.ByItem)
                {
                    UIItemPicker itemPicker = UIRoot.instance.uiGame.itemPicker;
                    if (!itemPicker.inited || itemPicker.active)
                    {
                        GUILayout.EndVertical();
                        return;
                    }

                    itemPicker.onReturn = SelectItem;
                    itemPicker._Open();
                }
            }

            GUILayout.EndVertical();
        }

        private static void DrawByItemButton()
        {
            GUILayout.BeginVertical();
            var clicked = GUILayout.Button(new GUIContent("Item", "Select item to show station list by item id"), GUILayout.MaxWidth(_windowRect.width / 5));
            if (clicked)
            {
                UIItemPicker itemPicker = UIRoot.instance.uiGame.itemPicker;
                if (!itemPicker.inited || itemPicker.active)
                {
                    GUILayout.EndVertical();
                    return;
                }

                itemPicker.onReturn = SelectItem;
                itemPicker._Open();
            }


            GUILayout.EndVertical();
        }

        private static void DrawByPlanetButton()
        {
            GUILayout.BeginVertical();
            var clicked = GUILayout.Button(new GUIContent("Planet", "Select planet to show station list "), GUILayout.MaxWidth(_windowRect.width / 5));
            if (clicked)
            {
                ShowPlanetPicker();
            }

            GUILayout.EndVertical();
        }

        private static void ShowPlanetPicker()
        {
            var planetInfos = LogisticsNetwork.byPlanet.Keys
                .Where(planId => LogisticsNetwork.byPlanet[planId].Count > 0)
                .Select(planId => new PlanetInfo { Name = GameMain.galaxy.PlanetById(planId).displayName, PlanetId = planId });
            if (_planetPickerWindow == null)
            {
                _planetPickerWindow = new PlanetPickerWindow(planetInfos);
            }
            else
            {
                _planetPickerWindow.SetItems(new List<PlanetInfo>(planetInfos));
            }

            _planetPickerWindow.visible = true;
        }

        private static void SelectItem(ItemProto obj)
        {
            if (obj != null)
            {
                _mode = Mode.ByItem;
                var list = LogisticsNetwork.byItem.ContainsKey(obj.ID) ? LogisticsNetwork.byItem[obj.ID] : new List<StationInfo>();
                _pager = new Pager<StationInfo>(list);
            }
        }

        private static void InitWindowRect()
        {
            var width = Mathf.Min(Screen.width, 650);
            var height = Screen.height < 560 ? Screen.height : 560;
            var offsetX = Mathf.RoundToInt((Screen.width - width) / 2f);
            var offsetY = Mathf.RoundToInt((Screen.height - height) / 2f);
            _windowRect = new Rect(offsetX, offsetY, width, height);

            Mathf.RoundToInt(_windowRect.width / 2.5f);
        }

        public static void DrawCenteredLabel(string text, params GUILayoutOption[] options)
        {
            GUILayout.BeginHorizontal(options);
            GUILayout.FlexibleSpace();
            GUILayout.Label(text);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(UIGame), "On_E_Switch")]
        public static bool UIGame_On_E_Switch_Prefix()
        {
            if (visible)
            {
                _requestHide = true;
                UIElements.CloseStationRequested = true;
            }

            return true;
        }
    }
}