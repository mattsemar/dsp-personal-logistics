using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using HarmonyLib;
using PersonalLogistics.Model;
using PersonalLogistics.PlayerInventory;
using PersonalLogistics.Util;
using UnityEngine;

namespace PersonalLogistics
{
    internal class Pager<T>
    {
        public int PageNum = 0;

        public int PageSize = 25;

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

    public class RequestWindow
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
        private static Pager<ItemProto> _pager;
        private static bool _dirty;
        private static bool _bannedHidden;
        private static EItemType _currentCategoryType = EItemType.Unknown;
        private static Dictionary<int, long> _toolTipAges = new Dictionary<int, long>();
        private static Dictionary<int, string> _toolTipCache = new Dictionary<int, string>();
        private static string[] _categoryNames;
        private static int _currentCategoryIndex = 0;
        private static List<(string seed, string stateString)> _otherSavedInventoryStateStrings;

        public static void OnClose()
        {
            visible = false;
            _requestHide = false;
            _pager = null;
            _bannedHidden = false;
            _currentCategoryIndex = 0;
            _currentCategoryType = EItemType.Unknown;
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

            _windowRect = GUILayout.Window(1297890112, _windowRect, WindowFnWrapper, "Personal Logistics Manager");
            EatInputInRect(_windowRect);
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
            else if (_dirty)
            {
                _dirty = false;
                var items = ItemUtil.GetAllItems().Where(item =>
                {
                    var banFilterResult = !_bannedHidden || !InventoryManager.Instance.IsBanned(item.ID);
                    var typeFilterResult = _currentCategoryType == EItemType.Unknown || _currentCategoryType == item.Type;
                    return banFilterResult && typeFilterResult;
                });
                _pager = new Pager<ItemProto>(items.ToList(), pageSize: 12);
            }

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
            GUILayout.BeginVertical("Box");
            {
                GUILayout.BeginHorizontal();
                DrawSaveInventoryButton();
                DrawCopyDesiredInventory();
                DrawClearRequestAndBans();
                DrawHideBanned();
                DrawPauseProcessing();
                GUILayout.EndHorizontal();


                if (_pager == null || _pager.IsFirst() && _pager.IsEmpty())
                {
                    GUILayout.Label($"No items");
                }
                else if (_pager != null)
                {
                    AddCategorySelector();
                    DrawCountLabel();
                    DrawPreviousButton();
                    var managedItems = _pager.GetPage();
                    // GUILayout.BeginVertical();
                    foreach (var item in managedItems)
                    {
                        var (minDesiredAmount, maxDesiredAmount) = InventoryManager.Instance == null ? (0, 0) : InventoryManager.Instance.GetDesiredAmount(item.ID);
                        var maxHeightSz = item.iconSprite.rect.height / 2;
                        var maxHeight = GUILayout.MaxHeight(maxHeightSz);
                        GUILayout.BeginHorizontal(maxHeight);
                        var rect = GUILayoutUtility.GetRect(maxHeightSz, maxHeightSz);
                        GUI.Label(rect, new GUIContent(item.iconSprite.texture, GetItemIconTooltip(item)));
                        if (maxDesiredAmount == int.MaxValue)
                        {
                            GUILayout.Label(new GUIContent("Unset", "This type of item will be ignored in inventory (not banished or requested)"));
                            // not requested or banned
                            var pressed = GUILayout.Button(new GUIContent("Ban", "Remove all of this item from inventory and add to logistics network"), maxHeight);
                            if (pressed)
                            {
                                if (InventoryManager.Instance != null)
                                {
                                    InventoryManager.Instance.BanItem(item.ID);
                                }
                            }

                            DrawSelectAmountSelector(item);
                        }
                        else if (maxDesiredAmount != 0 && minDesiredAmount > 0)
                        {
                            // currently requesting
                            GUILayout.Label(new GUIContent("Requested", "This type of item will be fetched from network if inventory count falls below requested amount"));
                            var pressed = GUILayout.Button(new GUIContent("Ban", "Remove all of this item from inventory and add to logistics network"));
                            if (pressed && InventoryManager.Instance != null)
                            {
                                InventoryManager.Instance.BanItem(item.ID);
                            }

                            if (InventoryManager.Instance != null)
                            {
                                DrawSelectAmountSelector(item);
                            }
                            else
                            {
                                DrawSelectAmountSelector(item);
                            }
                        }
                        else
                        {
                            // banned
                            GUILayout.Label(new GUIContent("Banned",
                                "This type of item will be removed from inventory and sent to the nearest logistics station with capacity for it"));
                            var pressed = GUILayout.Button(new GUIContent("Unban", "Remove ban and allow item to be in inventory"),
                                GUILayout.ExpandWidth(true));
                            var banned = true;
                            if (pressed)
                            {
                                banned = false;
                                if (InventoryManager.Instance != null)
                                {
                                    InventoryManager.Instance.UnBanItem(item.ID);
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
                var style = new GUIStyle
                {
                    normal = new GUIStyleState { textColor = Color.white },
                    wordWrap = true,
                    alignment = TextAnchor.MiddleCenter,
                    stretchHeight = true,
                    stretchWidth = true
                };

                var height = style.CalcHeight(new GUIContent(GUI.tooltip), _windowRect.width) + 10;
                var rect = GUILayoutUtility.GetRect(_windowRect.width - 20, height * 1.25f);
                GUI.Box(rect, GUI.tooltip, style);
            }
        }

        private static string GetItemIconTooltip(ItemProto item)
        {
            if (_toolTipCache.TryGetValue(item.ID, out string toolTip))
            {
                if (_toolTipAges.TryGetValue(item.ID, out long tipAge))
                {
                    long elapsedTicks = DateTime.Now.Ticks - tipAge;
                    TimeSpan elapsedSpan = new TimeSpan(elapsedTicks);
                    if (elapsedSpan < TimeSpan.FromSeconds(5))
                    {
                        return toolTip;
                    }
                }
            }

            var sb = new StringBuilder($"{item.Name.Translate()} - ");
            sb.Append(LogisticsNetwork.ShortItemSummary(item.ID));
            _toolTipAges[item.ID] = DateTime.Now.Ticks;
            _toolTipCache[item.ID] = sb.ToString();
            return sb.ToString();
        }

        private static void DrawSelectAmountSelector(ItemProto item)
        {
            if (InventoryManager.Instance == null)
                return;

            var minDesiredAmount = InventoryManager.Instance.GetDesiredAmount(item.ID).minDesiredAmount;
            var maxDesiredAmount = InventoryManager.Instance.GetDesiredAmount(item.ID).maxDesiredAmount;
            var strValMin = minDesiredAmount.ToString(CultureInfo.InvariantCulture);
            var strValMax = maxDesiredAmount == int.MaxValue ? "" : maxDesiredAmount.ToString(CultureInfo.InvariantCulture);
            // set max so you could fill your entire inventory
            // 120 slots * stackSz = 120 * 1000 = 120k foundation max, for example
            var maxAllowed = GameMain.mainPlayer.package.size * item.StackSize;
            {
                var strResult = GUILayout.TextField(strValMin, GUILayout.Width(150));
                GUILayout.Label(new GUIContent("Min", $"Maintain at least this many of this item in your inventory"));
                if (strResult != strValMin)
                {
                    try
                    {
                        var resultVal = (float)Convert.ToDouble(strResult, CultureInfo.InvariantCulture);
                        var clampedResultVal = Mathf.Clamp(resultVal, 1, maxAllowed);
                        InventoryManager.Instance.SetDesiredAmount(item.ID, (int)clampedResultVal, Math.Max((int)clampedResultVal, maxDesiredAmount));
                    }
                    catch (FormatException)
                    {
                        // Ignore user typing in bad data
                    }
                }
            }
            {
                var strResult = GUILayout.TextField(strValMax, GUILayout.Width(150));
                GUILayout.Label(new GUIContent("Max", $"Any items above this amount will be sent to your logistics network stations"));

                if (strResult != strValMax)
                {
                    try
                    {
                        var resultVal = (float)Convert.ToDouble(strResult, CultureInfo.InvariantCulture);
                        var clampedResultVal = Mathf.Clamp(resultVal, minDesiredAmount, maxAllowed);
                        InventoryManager.Instance.SetDesiredAmount(item.ID, minDesiredAmount, (int)clampedResultVal);
                    }
                    catch (FormatException)
                    {
                        // Ignore user typing in bad data
                    }
                }
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
                    _pager.Previous();
            }


            GUILayout.EndHorizontal();
        }

        private static void DrawSaveInventoryButton()
        {
            var guiContent = new GUIContent("Copy Inventory", "Use current inventory amounts to set requested/banned items.");

            GUILayout.BeginVertical("Box");

            int currentlySelected = 0;
            var clicked = GUILayout.Button(guiContent, GUILayout.ExpandWidth(false));

            if (clicked)
            {
                InventoryManager.Instance.SaveInventoryAsDesiredState();
                _dirty = true;
            }

            GUILayout.EndVertical();
        }

        private static void DrawClearRequestAndBans()
        {
            var guiContent = new GUIContent("Clear", "Clear all requests and bans");

            GUILayout.BeginVertical("Box");

            int currentlySelected = 0;
            var clicked = GUILayout.Button(guiContent, GUILayout.ExpandWidth(false));

            if (clicked)
            {
                InventoryManager.Instance.Clear();
                _dirty = true;
            }

            GUILayout.EndVertical();
        }

        private static void DrawCopyDesiredInventory()
        {
            if (_otherSavedInventoryStateStrings == null)
                _otherSavedInventoryStateStrings = CrossSeedInventoryState.GetStatesForOtherSeeds(GameUtil.GetSeed());
            if (_otherSavedInventoryStateStrings.Count < 1)
            {
                return;
            }

            foreach (var valueTuple in _otherSavedInventoryStateStrings)
            {
                var guiContent = new GUIContent("Copy", $"Copy saved inventory state from other game seed ({valueTuple.seed})");

                GUILayout.BeginVertical("Box");

                int currentlySelected = 0;
                var clicked = GUILayout.Button(guiContent, GUILayout.ExpandWidth(false));

                if (clicked)
                {
                    InventoryManager.Instance.SaveDesiredStateFromOther(valueTuple.stateString);
                    _dirty = true;
                }

                GUILayout.EndVertical();
            }
        }

        private static void DrawHideBanned()
        {
            var text = _bannedHidden ? "Show banned" : "Hide banned";
            var tip = _bannedHidden ? "Show all items (including banned)" : "Filter out banned items";
            var guiContent = new GUIContent(text, tip);

            GUILayout.BeginVertical("Box");

            int currentlySelected = 0;
            var clicked = GUILayout.Button(guiContent, GUILayout.ExpandWidth(false));

            if (clicked)
            {
                _bannedHidden = !_bannedHidden;
                _dirty = true;
            }

            GUILayout.EndVertical();
        }

        private static void DrawPauseProcessing()
        {
            var text = PluginConfig.inventoryManagementPaused.Value ? "Resume" : "Pause";
            var tip = PluginConfig.inventoryManagementPaused.Value ? "Resume personal logistics system" : "Pause personal logistics system";
            var guiContent = new GUIContent(text, tip);

            GUILayout.BeginVertical("Box");

            int currentlySelected = 0;
            var clicked = GUILayout.Button(guiContent, GUILayout.ExpandWidth(false));

            if (clicked)
            {
                PluginConfig.inventoryManagementPaused.Value = !PluginConfig.inventoryManagementPaused.Value;
            }

            GUILayout.EndVertical();
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

        private static void AddCategorySelector()
        {
            var names = GetCategoryNames();
            
            var selectedName = Enum.GetName(typeof(EItemType), _currentCategoryType);
            if (selectedName == "Unknown")
                selectedName = "All";

            var guiContents = names.Select(n => GetCategoryAsGuiContent(n, "Filter list by item type", selectedName == n));
            GUILayout.BeginHorizontal();
            GUILayout.Label("Filter");
            GUILayout.BeginVertical("Box");

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
                    _dirty = true;
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
                        _dirty = true;
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
                return _categoryNames;
            var names = Enum.GetNames(typeof(EItemType));
            names[0] = "All";
            var allItemTypes = ItemUtil.GetAllItemTypes();
            var result = new List<string>();
            result.Add("All");
            for (int i = 0; i < names.Length; i++)
            {
                if (Enum.TryParse(names[i], out EItemType enumVal))
                {
                    if (allItemTypes.Contains(enumVal))
                    {
                        result.Add(names[i]);
                    }
                }
            }

            _categoryNames = result.ToArray();
            return _categoryNames;
        }

        private static GUIContent GetCategoryAsGuiContent(string sourceValue, string parentDescription, bool currentlySelected)
        {
            var currentlySelectedIndicator = currentlySelected ? "<b>(selected)</b> " : "";
            var label = currentlySelected ? $"<b>{sourceValue}</b>" : sourceValue;
            return new GUIContent(label, $"<b>{parentDescription}</b> {currentlySelectedIndicator} {sourceValue}");
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
            }

            return true;
        }

        // copied from https://github.com/starfi5h/DSP_Mod/blob/d38b52eb895d43e6feee09e6bb537a5726d7d466/SphereEditorTools/UIWindow.cs#L221
        public static void EatInputInRect(Rect eatRect)
        {
            if (!(Input.GetMouseButton(0) || Input.GetMouseButtonDown(0))) //Eat only when left-click
                return;
            if (eatRect.Contains(new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y)))
                Input.ResetInputAxes();
        }

        public static void Reset()
        {
            _otherSavedInventoryStateStrings = null;
        }
    }
}