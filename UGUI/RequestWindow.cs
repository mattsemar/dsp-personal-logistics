using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using HarmonyLib;
using PersonalLogistics.Logistics;
using PersonalLogistics.Model;
using PersonalLogistics.PlayerInventory;
using PersonalLogistics.Scripts;
using PersonalLogistics.UI;
using PersonalLogistics.Util;
using UnityEngine;
using static PersonalLogistics.UI.UiScaler;

namespace PersonalLogistics.UGUI
{
    public enum Mode
    {
        [Description("Requested Items")] RequestWindow,
        [Description("Buffered Items")] BufferState,
        [Description("Config")] ConfigWindow,
        [Description("Actions")] ActionsWindow
    }

    public class RequestWindow
    {
        private static bool _requestHide;
        public static Rect windowRect = ScaleRectToDefault(300, 150, 600, 800);

        public static bool NeedReinit;

        private static Texture2D _tooltipBg;

        private static int _loggedMessageCount = 0;
        private static string _savedGUISkin;
        private static GUISkin _savedGUISkinObj;
        private static Color _savedColor;
        private static GUIStyle _savedTextStyle;
        private static Color _savedBackgroundColor;
        private static Color _savedContentColor;
        private static GUISkin _mySkin;
        private static int _chosenPlanet = -1;
        private static Pager<ItemProto> _pager;
        public static bool dirty;
        public static bool bufferWindowDirty;
        private static bool _bannedHidden;
        private static EItemType _currentCategoryType = EItemType.Unknown;
        private static readonly Dictionary<int, long> toolTipAges = new Dictionary<int, long>();
        private static readonly Dictionary<int, string> toolTipCache = new Dictionary<int, string>();
        private static string[] _categoryNames;
        private static int _currentCategoryIndex;
        private static List<(string seed, string stateString)> _otherSavedInventoryStateStrings;
        public static Mode mode = Mode.RequestWindow;
        private static GUIStyle _textStyle;
        private static readonly int _defaultFontSize = ScaleToDefault(12);
        public static bool Visible { get; set; }
        public static GUIStyle toolTipStyle { get; private set; }

        public static void OnClose()
        {
            Visible = false;
            mode = Mode.RequestWindow;
            _requestHide = false;
            bufferWindowDirty = true;
            RestoreGuiSkinOptions();
        }

        public static void OnGUI()
        {
            if (Input.GetKeyDown(KeyCode.Escape) || _requestHide)
            {
                OnClose();
                return;
            }

            Init();
            switch (mode)
            {
                case Mode.RequestWindow:
                    windowRect = GUILayout.Window(1297890112, windowRect, WindowFnWrapper, "Personal Logistics Manager");
                    break;
                case Mode.BufferState:
                    windowRect = GUILayout.Window(1297890113, windowRect, BufferStateWindow.WindowFunction, "Buffered items");
                    break;
                case Mode.ConfigWindow:
                    windowRect = GUILayout.Window(1297890115, windowRect, ConfigWindow.WindowFunction, "Config");
                    break;
                case Mode.ActionsWindow:
                    windowRect = GUILayout.Window(1297890116, windowRect, ActionWindow.WindowFunction, "Actions");
                    break;
            }

            EatInputInRect(windowRect);
        }

        public static void RestoreGuiSkinOptions()
        {
            GUI.skin = _savedGUISkinObj;
            GUI.backgroundColor = _savedBackgroundColor;
            GUI.contentColor = _savedContentColor;
            GUI.color = _savedColor;
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
                if (_textStyle == null)
                {
                    _textStyle = new GUIStyle(GUI.skin.label)
                    {
                        alignment = TextAnchor.MiddleCenter,
                        fontSize = _defaultFontSize
                    };
                }

                GUI.skin.label = _textStyle;
                GUI.skin.button = new GUIStyle(GUI.skin.button)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = _defaultFontSize
                };
                GUI.skin.textField = new GUIStyle(GUI.skin.textField)
                {
                    fontSize = _defaultFontSize
                };
                GUI.skin.toggle = new GUIStyle(GUI.skin.toggle)
                {
                    fontSize = _defaultFontSize,
                    alignment = TextAnchor.MiddleCenter
                };
            }
            else
            {
                _savedGUISkinObj = GUI.skin;
                GUI.skin = _mySkin;
            }
        }

        private static void Init()
        {
            if (_tooltipBg == null && !NeedReinit)
            {
                return;
            }

            var background = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            background.SetPixel(0, 0, Color.black);
            background.Apply();
            _tooltipBg = background;
            InitWindowRect();
            NeedReinit = false;
        }

        private static void WindowFnWrapper(int id)
        {
            if (_pager == null)
            {
                _pager = new Pager<ItemProto>(ItemUtil.GetAllItems(), 12);
            }
            else if (dirty)
            {
                dirty = false;
                var items = ItemUtil.GetAllItems().Where(item =>
                {
                    var banFilterResult = !_bannedHidden || !InventoryManager.instance.IsBanned(item.ID);
                    var typeFilterResult = _currentCategoryType == EItemType.Unknown || _currentCategoryType == item.Type;
                    return banFilterResult && typeFilterResult;
                });
                _pager = new Pager<ItemProto>(items.ToList(), 12);
            }

            SaveCurrentGuiOptions();
            WindowFn();
            GUI.DragWindow();
            RestoreGuiSkinOptions();
        }

        private static void WindowFn()
        {
            GUILayout.BeginArea(new Rect(windowRect.width - 25f, 0f, 25f, 30f));
            if (GUILayout.Button("X"))
            {
                OnClose();
                return;
            }

            GUILayout.EndArea();
            GUILayout.BeginVertical("Box");
            {
                DrawModeSelector();

                if (_pager == null || _pager.IsFirst() && _pager.IsEmpty())
                {
                    AddCategorySelector();
                    GUILayout.Label("No items");
                }
                else if (_pager != null)
                {
                    AddCategorySelector();
                    DrawCountLabel();
                    DrawPreviousButton();
                    var managedItems = _pager.GetPage();
                    foreach (var item in managedItems)
                    {
                        var (minDesiredAmount, maxDesiredAmount, _) = InventoryManager.instance == null ? (0, 0, true) : InventoryManager.instance.GetDesiredAmount(item.ID);
                        var maxHeightSz = ScaleToDefault((int)item.iconSprite.rect.height / 2);
                        var maxHeight = GUILayout.MaxHeight(maxHeightSz);
                        GUILayout.BeginHorizontal(_textStyle, maxHeight);
                        var rect = GUILayoutUtility.GetRect(maxHeightSz, maxHeightSz);
                        GUI.Label(rect, new GUIContent(item.iconSprite.texture, GetItemIconTooltip(item)));
                        if (maxDesiredAmount == int.MaxValue && minDesiredAmount < 1)
                        {
                            GUILayout.Label(new GUIContent("Unset", "This type of item will be ignored in inventory (not banished or requested)"), GUI.skin.label);
                            // not requested or banned
                            var pressed = GUILayout.Button(new GUIContent("Ban", "Remove all of this item from inventory and add to logistics network"), GUI.skin.button,
                                GUILayout.ExpandWidth(true));
                            if (pressed)
                            {
                                if (InventoryManager.instance != null)
                                {
                                    InventoryManager.instance.BanItem(item.ID);
                                }
                            }

                            DrawSelectAmountSelector(item);
                        }
                        else if (minDesiredAmount > 0)
                        {
                            // currently requesting
                            GUILayout.Label(new GUIContent("Requested", "This type of item will be fetched from network if inventory count falls below requested amount"),
                                GUI.skin.label);
                            var pressed = GUILayout.Button(new GUIContent("Ban", "Remove all of this item from inventory and add to logistics network"), GUI.skin.button,
                                GUILayout.ExpandWidth(true));
                            if (pressed && InventoryManager.instance != null)
                            {
                                InventoryManager.instance.BanItem(item.ID);
                            }

                            DrawSelectAmountSelector(item);
                        }
                        else
                        {
                            // banned
                            GUILayout.Label(new GUIContent("Banned",
                                "This type of item will be removed from inventory and sent to the nearest logistics station with capacity for it"), GUI.skin.label);
                            var pressed = GUILayout.Button(new GUIContent("Unban", "Remove ban and allow item to be in inventory"), GUI.skin.button,
                                GUILayout.ExpandWidth(true));
                            var banned = true;
                            if (pressed)
                            {
                                if (InventoryManager.instance != null)
                                {
                                    InventoryManager.instance.UnBanItem(item.ID);
                                }
                            }

                            DrawSelectAmountSelector(item);
                        }

                        GUILayout.EndHorizontal();
                    }

                    DrawNextButton();
                }
            }
            GUILayout.EndVertical();
            if (GUI.tooltip != null)
            {
                GUI.skin = null;
                if (toolTipStyle == null)
                {
                    toolTipStyle = new GUIStyle
                    {
                        normal = new GUIStyleState { textColor = Color.white },
                        wordWrap = true,
                        alignment = TextAnchor.MiddleCenter,
                        stretchHeight = true,
                        stretchWidth = true,
                        fontSize = _defaultFontSize
                    };
                }

                var height = toolTipStyle.CalcHeight(new GUIContent(GUI.tooltip), windowRect.width) + 10;
                var rect = GUILayoutUtility.GetRect(windowRect.width - 20, height * 1.25f);
                rect.y += 20;
                GUI.Box(rect, GUI.tooltip, toolTipStyle);
            }
        }

        public static string GetItemIconTooltip(ItemProto item)
        {
            if (toolTipCache.TryGetValue(item.ID, out var toolTip))
            {
                if (toolTipAges.TryGetValue(item.ID, out var tipAge))
                {
                    var elapsedTicks = DateTime.Now.Ticks - tipAge;
                    var elapsedSpan = new TimeSpan(elapsedTicks);
                    if (elapsedSpan < TimeSpan.FromSeconds(5))
                    {
                        return toolTip;
                    }
                }
            }

            var sb = new StringBuilder($"{item.Name.Translate()} - ");
            sb.Append(LogisticsNetwork.ShortItemSummary(item.ID));
            toolTipAges[item.ID] = DateTime.Now.Ticks;
            toolTipCache[item.ID] = sb.ToString();
            return sb.ToString();
        }

        private static void DrawSelectAmountSelector(ItemProto item)
        {
            if (InventoryManager.instance == null)
            {
                return;
            }

            var (minDesiredAmount, maxDesiredAmount, _) = InventoryManager.instance.GetDesiredAmount(item.ID);
            var strValMin = minDesiredAmount.ToString(CultureInfo.InvariantCulture);
            var strValMax = maxDesiredAmount == int.MaxValue ? "" : maxDesiredAmount.ToString(CultureInfo.InvariantCulture);

            // set max so you could fill your entire inventory
            // 120 slots * stackSz = 120 * 1000 = 120k foundation max, for example
            _textStyle.CalcMinMaxWidth(new GUIContent(1_000_000.ToString()), out var minWidth, out var maxWidth);
            var maxAllowed = GameMain.mainPlayer.package.size * item.StackSize;
            {
                GUILayout.Label(new GUIContent("Min", "Maintain at least this many of this item in your inventory"), _textStyle);

                var strResult = GUILayout.TextField(strValMin, 5, GUI.skin.textField, GUILayout.MinWidth(minWidth), GUILayout.MaxWidth(maxWidth));
                // GUILayout.EndHorizontal();
                if (strResult != strValMin)
                {
                    try
                    {
                        var resultVal = (float)Convert.ToDouble(strResult, CultureInfo.InvariantCulture);
                        var clampedResultVal = Mathf.Clamp(resultVal, 1, maxAllowed);
                        InventoryManager.instance.SetDesiredAmount(item.ID, (int)clampedResultVal, Math.Max((int)clampedResultVal, maxDesiredAmount));
                    }
                    catch (FormatException)
                    {
                        // Ignore user typing in bad data
                    }
                }
            }
            {
                GUILayout.Label(new GUIContent("Max", "Any items above this amount will be sent to your logistics network stations"), _textStyle);
                var strResult = GUILayout.TextField(strValMax, 7, GUILayout.MinWidth(minWidth), GUILayout.MaxWidth(maxWidth));
                if (strResult != strValMax)
                {
                    try
                    {
                        var resultVal = (float)Convert.ToDouble(strResult, CultureInfo.InvariantCulture);
                        var clampedResultVal = Mathf.Clamp(resultVal, minDesiredAmount, maxAllowed);
                        InventoryManager.instance.SetDesiredAmount(item.ID, minDesiredAmount, (int)clampedResultVal);
                    }
                    catch (FormatException)
                    {
                        // Ignore user typing in bad data
                    }
                }
            }
            if (minDesiredAmount > 0)
            {
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
            {
                return;
            }

            GUILayout.BeginHorizontal();
            var buttonPressed = GUILayout.Button(new GUIContent("Next", "Load next page of items"));

            if (buttonPressed)
            {
                _pager.Next();
            }

            GUILayout.EndHorizontal();
        }

        private static void DrawPreviousButton()
        {
            var disabled = _pager.IsFirst();
            if (disabled)
            {
                DrawNextButton();
                return;
            }

            GUILayout.BeginHorizontal();

            var buttonPressed = GUILayout.Button(new GUIContent("Previous", "Load previous page"));
            if (buttonPressed)
            {
                if (!_pager.IsFirst())
                {
                    _pager.Previous();
                }
            }


            GUILayout.EndHorizontal();
        }

        public static void DrawModeSelector()
        {
            var names = Enum.GetNames(typeof(Mode));
            var selectedName = Enum.GetName(typeof(Mode), mode);
            var guiContents = names.Select(n => GetModeAsGuiContent(n, "Switch mode", selectedName == n));
            GUILayout.BeginHorizontal();
            GUILayout.Label("Mode");
            GUILayout.BeginVertical("Box");
            var curIndex = names.ToList().IndexOf(selectedName);
            var index = GUILayout.Toolbar(curIndex, guiContents.ToArray());

            if (index != curIndex)
            {
                if (Enum.TryParse(names[index], out Mode newMode))
                {
                    mode = newMode;
                    dirty = true;
                }
            }

            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }


        public static void DrawCopyDesiredInventory()
        {
            if (!PluginConfig.enableCopyGame.Value)
            {
                return;
            }

            if (_otherSavedInventoryStateStrings == null)
            {
                _otherSavedInventoryStateStrings = CrossSeedInventoryState.GetStatesForOtherSeeds(GameUtil.GetSeed());
            }

            if (_otherSavedInventoryStateStrings.Count < 1)
            {
                return;
            }

            foreach (var valueTuple in _otherSavedInventoryStateStrings)
            {
                var guiContent = new GUIContent("Copy Game", $"Copy saved inventory state from other game seed ({valueTuple.seed})");

                GUILayout.BeginVertical("Box");

                var currentlySelected = 0;
                var clicked = GUILayout.Button(guiContent, GUILayout.ExpandWidth(false));

                if (clicked)
                {
                    InventoryManager.instance.SaveDesiredStateFromOther(valueTuple.stateString);
                    dirty = true;
                }

                GUILayout.EndVertical();
            }
        }

        private static void DrawHideBanned()
        {
            var text = _bannedHidden ? "Show banned" : "Hide banned";
            var tip = _bannedHidden ? "Show all items (including banned)" : "Filter out banned items";
            var guiContent = new GUIContent(text, tip);

            // GUILayout.BeginVertical("Box");

            var currentlySelected = 0;
            var clicked = GUILayout.Button(guiContent);

            if (clicked)
            {
                _bannedHidden = !_bannedHidden;
                dirty = true;
            }

            // GUILayout.EndVertical();
        }

        private static void InitWindowRect()
        {
            var width = Mathf.Min(Screen.width, ScaleToDefault(650));
            var height = Screen.height < 560 ? Screen.height : ScaleToDefault(560, false);
            var offsetX = Mathf.RoundToInt((Screen.width - width) / 2f);
            var offsetY = Mathf.RoundToInt((Screen.height - height) / 2f);
            windowRect = new Rect(offsetX, offsetY, width, height);

            Mathf.RoundToInt(windowRect.width / 2.5f);
        }

        private static void AddCategorySelector()
        {
            var names = GetCategoryNames();

            var selectedName = Enum.GetName(typeof(EItemType), _currentCategoryType);
            if (selectedName == "Unknown")
            {
                selectedName = "All";
            }

            var guiContents = names.Select(n => GetModeAsGuiContent(n, "Filter list by item type", selectedName == n));
            GUILayout.BeginHorizontal();
            GUILayout.Label("Filter");
            GUILayout.BeginVertical("Box");
            DrawHideBanned();

            var index = GUILayout.Toolbar(_currentCategoryIndex, guiContents.ToArray());

            if (_currentCategoryIndex != index)
            {
                SetNewCategorySelection(index);
            }

            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        private static void SetNewCategorySelection(int index)
        {
            var categoryNames = GetCategoryNames();
            if (index < 0 || index >= categoryNames.Length)
            {
                Log.Warn($"invalid index {index} for category selection");
                return;
            }

            _currentCategoryIndex = index;
            var categoryName = categoryNames[index];
            if (categoryName == "All")
            {
                if (_currentCategoryType != EItemType.Unknown)
                {
                    _currentCategoryType = EItemType.Unknown;
                    dirty = true;
                }
                else
                {
                    Log.Warn($"new selected category {EItemType.Unknown} same as before {_currentCategoryType}");
                }
            }
            else
            {
                if (Enum.TryParse(categoryNames[index], out EItemType newSelected))
                {
                    if (_currentCategoryType == newSelected)
                    {
                        Log.Warn($"new selected category {newSelected} same as before {_currentCategoryType}");
                    }
                    else
                    {
                        dirty = true;
                        _currentCategoryType = newSelected;
                    }
                }
                else
                {
                    Log.Warn($"failed to parse category name {categoryNames[index]} into EItem type");
                }
            }
        }

        private static string[] GetCategoryNames()
        {
            if (_categoryNames != null)
            {
                return _categoryNames;
            }

            var names = Enum.GetNames(typeof(EItemType));
            names[0] = "All";
            var allItemTypes = ItemUtil.GetAllItemTypes();
            var result = new List<string> { "All" };
            foreach (var name in names)
            {
                if (Enum.TryParse(name, out EItemType enumVal))
                {
                    if (allItemTypes.Contains(enumVal))
                    {
                        result.Add(name);
                    }
                }
            }

            _categoryNames = result.ToArray();
            return _categoryNames;
        }

        private static GUIContent GetModeAsGuiContent(string sourceValue, string parentDescription, bool currentlySelected)
        {
            var enumMember = typeof(Mode).GetMember(sourceValue).FirstOrDefault();
            var attr = enumMember?.GetCustomAttributes(typeof(DescriptionAttribute), false).Cast<DescriptionAttribute>().FirstOrDefault();
            var currentlySelectedIndicator = currentlySelected ? "<b>(selected)</b> " : "";
            var sval = attr?.Description ?? sourceValue;
            var label = currentlySelected ? $"<b>{sval}</b>" : sval;
            return new GUIContent(label, $"<b>{parentDescription}</b> {currentlySelectedIndicator} {sval}");
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
            if (Visible)
            {
                _requestHide = true;
                return true;
            }

            if (VFInput.control)
            {
                if (PluginConfig.useLegacyRequestWindowUI.Value)
                    Visible = !Visible;
                return false;
            }

            if (RequesterWindow.Instance != null)
            {
                RequesterWindow.Instance.Hide();
            }

            return true;
        }

        // copied from https://github.com/starfi5h/DSP_Mod/blob/d38b52eb895d43e6feee09e6bb537a5726d7d466/SphereEditorTools/UIWindow.cs#L221
        public static void EatInputInRect(Rect eatRect)
        {
            if (!(Input.GetMouseButton(0) || Input.GetMouseButtonDown(0))) //Eat only when left-click
            {
                return;
            }

            if (eatRect.Contains(new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y)))
            {
                Input.ResetInputAxes();
            }
        }

        public static void Reset()
        {
            _otherSavedInventoryStateStrings = null;
        }
    }
}