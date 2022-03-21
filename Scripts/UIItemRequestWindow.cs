using System;
using System.Linq;
using CommonAPI;
using CommonAPI.Systems;
using HarmonyLib;
using PersonalLogistics.Model;
using PersonalLogistics.ModPlayer;
using PersonalLogistics.UGUI;
using PersonalLogistics.Util;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PersonalLogistics.Scripts
{
    public class UIItemRequestWindow : ManualBehaviour
    {
        private bool overrideMats = true;
        private const int colCount = 12;
        private const int recipeRowCount = 7;
        private const int kGridSize = 46;
        [SerializeField] public RectTransform windowRect;
        [SerializeField] public RectTransform itemGroup;
        [SerializeField] public Image itemBg;
        [SerializeField] public RawImage recipeIcons;
        [SerializeField] public Image recipeSelImage;
        [SerializeField] public UIButton typeButton1;
        [SerializeField] public UIButton typeButton2;
        private UITabButton[] _otherTypeButtons;
        [SerializeField] public UIButton minPlusButton;
        [SerializeField] public UIButton minMinusButton;
        [SerializeField] public UIButton maxPlusButton;
        [SerializeField] public UIButton maxMinusButton;
        [SerializeField] public UIButton confirmButton;
        [SerializeField] public Text multiValueText;
        [SerializeField] public Text multiValueMaxText;
        [SerializeField] public bool showTips = true;
        [SerializeField] public float showTipsDelay = 0.4f;
        [SerializeField] public int tipAnchor = 7;
        [SerializeField] public Image selectItemIcon;
        [SerializeField] public Text selectedItemCurrentState;
        [SerializeField] public Text selectedItemRequestSummary;
        [SerializeField] public UIButton pauseButton;
        [SerializeField] public UIButton playButton;
        [SerializeField] public Text prefabNumText;
        [SerializeField] public Text prefabNumRecycleText;

        private UIItemTip screenTip;
        private float mouseInTime;
        private EventTrigger eventTriggerItem;
        private bool requestAmountChanged;
        private int currentType = 1;
        public Material recipeIconMat;
        public Material recipeBgMat;
        public uint[] itemIndexArray;
        public uint[] itemStateArray;
        private ItemProto[] itemProtoArray;
        public ComputeBuffer itemIndexBuffer;
        public ComputeBuffer itemStateBuffer;
        private ItemProto selectedItem;
        private int currentRequestMin;
        private int currentRequestMax;

        private StorageComponent _test_package;
        private bool mouseInItemAreas;
        private int mouseItemIndex = -1;
        public Text[] numTexts = new Text[400];
        public Text[] maxTexts = new Text[400];

        private static readonly int buffer = Shader.PropertyToID("_StateBuffer");
        private static readonly int indexBuffer = Shader.PropertyToID("_IndexBuffer");

        public override void _OnCreate()
        {
            Log.Debug($"_OnCreate() {GetType()}");
            itemIndexArray = new uint[1000];
            itemIndexBuffer = new ComputeBuffer(itemIndexArray.Length, 4);
            itemStateArray = new uint[1000];
            itemStateBuffer = new ComputeBuffer(itemStateArray.Length, 4);
            itemProtoArray = new ItemProto[1000];
            itemBg.material = recipeBgMat;
            recipeIcons.material = recipeIconMat;
            SetMaterialProps();
            typeButton1.data = 1;
            typeButton2.data = 2;
            eventTriggerItem = itemBg.gameObject.AddComponent<EventTrigger>();
            {
                EventTrigger.Entry pointerDown = new EventTrigger.Entry();
                pointerDown.eventID = EventTriggerType.PointerDown;
                pointerDown.callback.AddListener(OnItemMouseDown);
                eventTriggerItem.triggers.Add(pointerDown);
            }
            {
                EventTrigger.Entry pointerEnter = new EventTrigger.Entry();
                pointerEnter.eventID = EventTriggerType.PointerEnter;
                pointerEnter.callback.AddListener(OnItemMouseEnter);
                eventTriggerItem.triggers.Add(pointerEnter);
            }
            {
                EventTrigger.Entry pointerExit = new EventTrigger.Entry();
                pointerExit.eventID = EventTriggerType.PointerExit;
                eventTriggerItem.triggers.Add(pointerExit);
                pointerExit.callback.AddListener(OnItemMouseExit);
            }
            {
                EventTrigger.Entry mouseClickTrigger = new EventTrigger.Entry();
                mouseClickTrigger.eventID = EventTriggerType.PointerDown;
                mouseClickTrigger.callback.AddListener(OnItemMouseDown);
                eventTriggerItem.triggers.Add(mouseClickTrigger);
            }

            currentRequestMax = Int32.MaxValue;
            currentRequestMin = 0;
            var tabs = TabSystem.GetAllTabs().ToList().FindAll(tab => tab != null);
            if (tabs.Count < 1)
            {
                Log.Debug($"No tabs to load");
                return;
            }

            _otherTypeButtons = new UITabButton[tabs.Count];
            for (int i = 0; i < tabs.Count; i++)
            {
                TabData tab = tabs[i];
                Log.Debug($"Adding tab custom tab: {tab.tabName.Translate()}");
                GameObject button = Instantiate(TabSystem.GetTabPrefab(), itemGroup.transform, false);
                ((RectTransform)button.transform).anchoredPosition = new Vector2(-25 + 70 * (i + 2), 50);
                UITabButton tabButton = button.GetComponent<UITabButton>();
                Sprite sprite = Resources.Load<Sprite>(tab.tabIconPath);
                tabButton.Init(sprite, tab.tabName, tab.tabIndex, OnTypeButtonClick);
                _otherTypeButtons[i] = tabButton;
            }
        }

        public override void _OnDestroy()
        {
            Log.Debug($"_OnDestroy() {GetType()}");

            if (screenTip != null)
            {
                Destroy(screenTip.gameObject);
                screenTip = null;
            }

            Destroy(recipeBgMat);
            Destroy(recipeIconMat);
            itemStateBuffer.Release();
            itemIndexBuffer.Release();
            recipeBgMat = null;
            recipeIconMat = null;
            itemStateBuffer = null;
            itemIndexBuffer = null;
            // numTexts = null;
        }

        public override bool _OnInit()
        {
            Log.Debug($"_OnInit() {GetType()}");
            SetSelectedItemIndex(-1, true);
            Array.Clear(itemIndexArray, 0, itemIndexArray.Length);
            Array.Clear(itemStateArray, 0, itemStateArray.Length);
            Array.Clear(itemProtoArray, 0, itemProtoArray.Length);
            recipeIcons.texture = GameMain.iconSet.texture;
            return true;
        }

        public override void _OnFree()
        {
            SetSelectedItemIndex(-1, false);
            Array.Clear(itemIndexArray, 0, itemIndexArray.Length);
            Array.Clear(itemStateArray, 0, itemStateArray.Length);
            Array.Clear(itemProtoArray, 0, itemProtoArray.Length);
            recipeIcons.texture = null;
        }

        public override void _OnRegEvent()
        {
            typeButton1.onClick += OnTypeButtonClick;
            typeButton2.onClick += OnTypeButtonClick;
            if (_otherTypeButtons != null && _otherTypeButtons.Length > 0)
            {
                foreach (var uiTabButton in _otherTypeButtons)
                {
                    uiTabButton.button.onClick += OnTypeButtonClick;
                }
            }
        }

        public override void _OnUnregEvent()
        {
            typeButton1.onClick -= OnTypeButtonClick;
            typeButton2.onClick -= OnTypeButtonClick;
            if (_otherTypeButtons != null && _otherTypeButtons.Length > 0)
            {
                foreach (var uiTabButton in _otherTypeButtons)
                {
                    uiTabButton.button.onClick -= OnTypeButtonClick;
                }
            }
        }

        // 0 for pause, 1 for play
        public void OnPlayPauseClick(int playOrPause)
        {
            var play = playOrPause == 1;
            if (play)
            {
                playButton.gameObject.SetActive(false);
                pauseButton.gameObject.SetActive(true);
                PluginConfig.Play();
            }
            else
            {
                playButton.gameObject.SetActive(true);
                pauseButton.gameObject.SetActive(false);
                PluginConfig.Pause();
            }
        }

        public override void _OnOpen()
        {
            Array.Clear(itemIndexArray, 0, itemIndexArray.Length);
            Array.Clear(itemStateArray, 0, itemStateArray.Length);
            Array.Clear(itemProtoArray, 0, itemProtoArray.Length);
            OnTypeButtonClick(currentType);
            SetMaterialProps();
            SetBufferData();
            GameMain.history.onTechUnlocked += OnTechUnlocked;
            transform.SetAsLastSibling();
            SyncPlayPauseButtons();
        }

        private void SyncPlayPauseButtons()
        {
            if (pauseButton == null || playButton == null)
            {
                Log.Warn($"play button null ({playButton == null}) OR pause button null ({pauseButton == null}). can't sync button state");
                return;
            }

            if (PluginConfig.IsPaused())
            {
                pauseButton.gameObject.SetActive(false);
                playButton.gameObject.SetActive(true);
            }
            else
            {
                playButton.gameObject.SetActive(false);
                pauseButton.gameObject.SetActive(true);
            }
        }

        public override void _OnClose()
        {
            GameMain.history.onTechUnlocked -= OnTechUnlocked;
            if (screenTip != null)
                screenTip.gameObject.SetActive(false);
            OnItemMouseExit(null);

            Array.Clear(itemIndexArray, 0, itemIndexArray.Length);
            Array.Clear(itemStateArray, 0, itemStateArray.Length);
            Array.Clear(itemProtoArray, 0, itemProtoArray.Length);
            SetSelectedItemIndex(-1, false);
            currentRequestMax = 0;
            currentRequestMin = 0;
        }


        public override void _OnUpdate()
        {
            TestMouseItemIndex();
            if (Input.GetKeyDown(KeyCode.Equals) || Input.GetKeyDown(KeyCode.KeypadPlus))
                OnMinPlusButtonClick(0);
            if (Input.GetKeyDown(KeyCode.Minus) || Input.GetKeyDown(KeyCode.KeypadMinus))
                OnMinMinusButtonClick(0);

            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                OnOkButtonClick(0, true);
            SetBufferData();
            int num1 = 0;
            int maxShowing = 999;

            confirmButton.button.interactable = true;
            typeButton1.button.interactable = true;
            typeButton2.button.interactable = true;
            minMinusButton.button.interactable = true;
            minPlusButton.button.interactable = true;
            maxMinusButton.button.interactable = true;
            maxPlusButton.button.interactable = true;
            SyncPlayPauseButtons();

            if (!showTips)
                return;
            int num4 = -1;
            int num5 = -1;
            int id = 0;
            if (mouseItemIndex >= 0)
            {
                id = itemProtoArray[mouseItemIndex] == null ? 0 : itemProtoArray[mouseItemIndex].ID;
                num4 = mouseItemIndex % colCount;
                num5 = mouseItemIndex / colCount;
            }

            ItemProto itemProto = id == 0 ? null : LDB.items.Select(id);
            if (itemProto != null)
            {
                int itemId = itemProto.ID;
                mouseInTime += Time.deltaTime;
                if (mouseInTime <= (double)showTipsDelay)
                    return;
                if (screenTip == null)
                    screenTip = UIItemTip.Create(itemId, tipAnchor,
                        new Vector2(num4 * kGridSize + 15, -num5 * kGridSize - 50), itemBg.transform, 0, 0, UIButton.ItemTipType.Item);
                if (!screenTip.gameObject.activeSelf)
                {
                    screenTip.gameObject.SetActive(true);
                    screenTip.SetTip(itemId, tipAnchor, new Vector2(num4 * kGridSize + 15, -num5 * kGridSize - 50), itemBg.transform, 0, 0,
                        UIButton.ItemTipType.Item);
                }
                else
                {
                    if (screenTip.showingItemId == itemId)
                        return;
                    screenTip.SetTip(itemId, tipAnchor, new Vector2(num4 * kGridSize + 15, -num5 * kGridSize - 50), itemBg.transform, 0, 0, UIButton.ItemTipType.Item);
                }
            }
            else
            {
                if (mouseInTime > 0.0)
                    mouseInTime = 0.0f;
                if (!(screenTip != null))
                    return;
                screenTip.showingItemId = 0;
                screenTip.gameObject.SetActive(false);
            }

            if (selectedItem == null)
            {
                selectedItemRequestSummary.gameObject.SetActive(false);
            }
            else
            {
                if (requestAmountChanged)
                {
                    selectedItemRequestSummary.gameObject.SetActive(true);
                }
            }
        }

        private void SetMaterialProps()
        {
            if (overrideMats)
            {
                if (itemBg != null)
                {
                    recipeBgMat = ProtoRegistry.CreateMaterial("UI Ex/Storage Bg", "storage-bg", "#FFFFFFFF", null, new string[] { });
                    recipeBgMat.SetBuffer(buffer, itemStateBuffer);
                    itemBg.material = recipeBgMat;
                }
                else
                {
                    Log.Warn("BGImage is null!");
                }

                if (recipeIcons != null)
                {
                    recipeIconMat = ProtoRegistry.CreateMaterial("UI Ex/Storage Icons", "storage-icons", "#FFFFFFFF", null, new string[] { });
                    recipeIconMat.SetBuffer(indexBuffer, itemIndexBuffer);
                    recipeIcons.material = recipeIconMat;
                    recipeIcons.texture = GameMain.iconSet.texture;
                }
                else
                {
                    Log.Warn("recipeIcons is null!");
                }
            }
            else
            {
                recipeBgMat.SetBuffer(buffer, itemStateBuffer);
                recipeIconMat.SetBuffer(indexBuffer, itemIndexBuffer);
            }

            float num1 = 0.06521739f;
            float num2 = 1.15f;
            Vector4 vector4_1 = new Vector4(12f, 0.0f, 0.04f, 0.04f);
            Vector4 vector4_2 = new Vector4(num1, num1, num2, num2);
            vector4_1.y = 1f;
            vector4_1.y = 7f;
            recipeBgMat.SetVector("_Grid", vector4_1);
            recipeIconMat.SetVector("_Grid", vector4_1);
            recipeIconMat.SetVector("_Rect", vector4_2);
        }

        private void SetBufferData()
        {
            itemStateBuffer.SetData(itemStateArray);
            itemIndexBuffer.SetData(itemIndexArray);
        }

        public void RefreshItemIcons()
        {
            Array.Clear(itemIndexArray, 0, itemIndexArray.Length);
            Array.Clear(itemStateArray, 0, itemStateArray.Length);
            Array.Clear(itemProtoArray, 0, itemProtoArray.Length);
            GameHistoryData history = GameMain.history;
            ItemProto[] dataArray = LDB.items.dataArray;
            IconSet iconSet = GameMain.iconSet;
            var inventoryManager = PlogPlayerRegistry.LocalPlayer()?.inventoryManager;

            for (int i = 0; i < dataArray.Length; ++i)
            {
                var itemProto = dataArray[i];
                if (itemProto.GridIndex >= 1101 && history.ItemUnlocked(itemProto.ID))
                {
                    int itemType = itemProto.GridIndex / 1000;
                    int row = (itemProto.GridIndex - itemType * 1000) / 100 - 1;
                    int col = itemProto.GridIndex % 100 - 1;
                    if (row >= 0 && col >= 0 && row < recipeRowCount && col < colCount)
                    {
                        int pageIndex = row * colCount + col;
                        if (pageIndex >= 0 && pageIndex < itemIndexArray.Length && itemType == currentType)
                        {
                            itemIndexArray[pageIndex] = iconSet.itemIconIndex[itemProto.ID];
                            itemStateArray[pageIndex] = 0U;
                            itemProtoArray[pageIndex] = itemProto;

                            if (!PluginConfig.showAmountsInRequestWindow.Value)
                            {
                                DeactivateAllCounts();
                                continue;
                            }

                            if (inventoryManager == null)
                            {
                                Log.Debug("Can't set req amount graphic");
                                continue;
                            }

                            if (itemProto.ID == 0)
                                continue;
                            CreateGridGraphic(pageIndex);

                            var desiredItem = inventoryManager.GetDesiredItem(itemProto.ID);
                            var requestedStacks = desiredItem.RequestedStacks();
                            if (desiredItem.IsNonManaged())
                            {
                                // not managed, who wrote this stupid comment anyway?
                                numTexts[pageIndex].text = "";
                                numTexts[pageIndex].gameObject.SetActive(true);
                                maxTexts[pageIndex].gameObject.SetActive(false);
                            }
                            else if (desiredItem.IsBanned())
                            {
                                // banned, another useless comment
                                numTexts[pageIndex].gameObject.SetActive(false);
                                maxTexts[pageIndex].text = "0";
                                maxTexts[pageIndex].gameObject.SetActive(true);
                            }
                            else if (desiredItem.IsRecycle() && desiredItem.RequestedStacks() == 0)
                            {
                                // Not automatically requested, but auto-recycled over a certain amount
                                numTexts[pageIndex].gameObject.SetActive(false);
                                maxTexts[pageIndex].text = desiredItem.RecycleMaxStacks().ToString();
                                maxTexts[pageIndex].gameObject.SetActive(true);
                            }
                            else
                            {
                                // Requested, and possibly auto-recycled but we can only show so much in 1 UI
                                numTexts[pageIndex].text = requestedStacks.ToString();
                                numTexts[pageIndex].gameObject.SetActive(true);
                                maxTexts[pageIndex].gameObject.SetActive(false);
                            }
                        }
                    }
                }
            }
        }

        public void OnTypeButtonClick(int type)
        {
            SetSelectedItemIndex(-1, true);
            currentType = type;
            DeactivateAllCounts();
            RefreshItemIcons();
            typeButton1.highlighted = type == 1;
            typeButton2.highlighted = type == 2;
            typeButton1.button.interactable = type != 1;
            typeButton2.button.interactable = type != 2;
            if (_otherTypeButtons == null || _otherTypeButtons.Length <= 0)
            {
                return;
            }

            foreach (var otherTypeButton in _otherTypeButtons)
            {
                otherTypeButton.button.highlighted = type == otherTypeButton.button.data;
                otherTypeButton.button.button.interactable = type != otherTypeButton.button.data;
            }
        }

        private void DeactivateAllCounts()
        {
            for (int index = 0; index < numTexts.Length; ++index)
            {
                if (numTexts[index] != null && numTexts[index].gameObject != null && numTexts[index].gameObject.activeSelf)
                {
                    numTexts[index].text = "";
                    numTexts[index].gameObject.SetActive(false);
                }

                if (maxTexts[index] != null && maxTexts[index].gameObject != null && maxTexts[index].gameObject.activeSelf)
                {
                    maxTexts[index].text = "";
                    maxTexts[index].gameObject.SetActive(false);
                }
            }
        }

        public void OnMinPlusButtonClick(BaseEventData bed)
        {
            if (bed.selectedObject == minPlusButton.gameObject)
                OnMinPlusButtonClick(1);
        }

        public void OnMinPlusButtonClick(int whatever)
        {
            if (selectedItem == null)
                return;

            var inc = GetIncrement(1);

            if (currentRequestMin >= currentRequestMax)
            {
                currentRequestMax = currentRequestMin + inc;
            }

            currentRequestMin += inc;

            if (currentRequestMin >= GameMain.mainPlayer.package.size)
            {
                currentRequestMin = GameMain.mainPlayer.package.size;
                multiValueText.text = "Fill";
            }
            else
                multiValueText.text = $"{currentRequestMin}";

            if (currentRequestMax <= currentRequestMin)
            {
                currentRequestMax = currentRequestMin;
                multiValueMaxText.text = $"{currentRequestMax}";
            }

            requestAmountChanged = true;
            UpdateSummaryText();
        }

        private static int GetIncrement(int sign)
        {
            var inc = VFInput.control ? GameMain.mainPlayer.package.size : 1;
            if (VFInput.shift)
                inc = 5;
            return inc * sign;
        }

        private void UpdateSummaryText()
        {
            selectedItemRequestSummary.text = BuildSummaryText(currentRequestMin, currentRequestMax);
        }

        private void UpdateCurrentText(DesiredItem desiredItem)
        {
            if (desiredItem.IsNonManaged())
            {
                selectedItemCurrentState.text = BuildSummaryText(0, GameMain.mainPlayer.package.size, selectedItem.StackSize);
            }
            else
            {
                selectedItemCurrentState.text = BuildSummaryText(desiredItem.RequestedStacks(), desiredItem.RecycleMaxStacks(), selectedItem.StackSize);
            }
        }

        private string BuildSummaryText(int minReq, int maxRecycle, int stackSize = 0)
        {
            var stackSizeText = stackSize > 1 ? $" (stack size {stackSize})" : "";
            var minText = minReq == 0 ? "Do not add this item to your inventory" : $"Maintain at least {minReq} stacks of this item";
            var maxText = maxRecycle == 0 ? "Ban this item from your inventory" : $"Recycle when you have more than {maxRecycle} stacks in your inventory";
            if (maxRecycle >= GameMain.mainPlayer.package.size)
            {
                maxText = "Do not auto-recycle this item out of your inventory";
            }

            var result = $"{minText} {stackSizeText}\r\n{maxText}";
            if (minReq == 0 && maxRecycle == 0)
            {
                result = "(Banned) recycle this item immediately if found in inventory".Translate() + stackSizeText;
            }

            return result;
        }

        public void OnMinMinusButtonClick(BaseEventData bed)
        {
            if (bed.selectedObject == minMinusButton.gameObject)
                OnMinMinusButtonClick(1);
        }

        public void OnMinMinusButtonClick(int whatever)
        {
            if (selectedItem == null)
                return;
            if (currentRequestMin < 1)
                return;
            var increment = GetIncrement(-1);

            currentRequestMin = Math.Max(0, currentRequestMin + increment);
            multiValueText.text = $"{currentRequestMin}";
            requestAmountChanged = true;
            UpdateSummaryText();
        }

        public void OnMaxPlusButtonClick(int whatever)
        {
            if (selectedItem == null)
                return;
            var inc = GetIncrement(1);
            if (currentRequestMax < currentRequestMin)
                currentRequestMax = currentRequestMin;
            currentRequestMax += inc;
            if (currentRequestMax >= GameMain.mainPlayer.package.size || currentRequestMax < 0)
            {
                currentRequestMax = GameMain.mainPlayer.package.size;
                multiValueMaxText.text = "Inf";
            }
            else
                multiValueMaxText.text = $"{currentRequestMax}";

            requestAmountChanged = true;
            UpdateSummaryText();
        }

        public void OnMaxMinusButtonClick(int whatever)
        {
            if (selectedItem == null)
                return;
            var inc = GetIncrement(-1);
            currentRequestMax = Math.Max(currentRequestMax + inc, 0);
            if (currentRequestMax < currentRequestMin)
                currentRequestMax = currentRequestMin;
            if (currentRequestMax >= GameMain.mainPlayer.package.size)
            {
                currentRequestMax = GameMain.mainPlayer.package.size;
                multiValueMaxText.text = "Inf";
            }
            else
                multiValueMaxText.text = $"{currentRequestMax}";

            requestAmountChanged = true;
            UpdateSummaryText();
        }

        public void OnOkButtonClick(int whatever)
        {
            OnOkButtonClick(1, true);
        }

        public void OnOkButtonClick(int whatever, bool buttonEnable)
        {
            if (selectedItem == null)
                return;
            var maxItems = currentRequestMax * selectedItem.StackSize;
            if (currentRequestMax >= GameMain.mainPlayer.package.size)
                maxItems = Int32.MaxValue;
            Log.Debug($"Updating selected item amounts {selectedItem.ID} {currentRequestMin * selectedItem.StackSize} {maxItems}");
            PlogPlayerRegistry.LocalPlayer().inventoryManager.SetDesiredAmount(selectedItem.ID, currentRequestMin * selectedItem.StackSize, maxItems);
            RefreshItemIcons();
        }

        private void TestMouseItemIndex()
        {
            for (int index = 0; index < itemStateArray.Length; ++index)
                itemStateArray[index] &= 254U;
            mouseItemIndex = -1;
            Vector2 rectPoint;
            if (!mouseInItemAreas || !UIRoot.ScreenPointIntoRect(Input.mousePosition, itemBg.rectTransform, out rectPoint))
                return;
            int num1 = Mathf.FloorToInt(rectPoint.x / kGridSize);
            int num2 = Mathf.FloorToInt((float)(-(double)rectPoint.y / kGridSize));
            if (num1 < 0 || num2 < 0 || num1 >= colCount || num2 >= recipeRowCount)
                return;
            mouseItemIndex = num1 + num2 * colCount;
            if (itemProtoArray[mouseItemIndex] == null)
                return;
            itemStateArray[mouseItemIndex] |= 1U;
        }

        private void SetSelectedItemIndex(int index, bool notify)
        {
            ItemProto item = selectedItem;
            mouseItemIndex = index;
            selectedItem = (uint)index >= itemProtoArray.Length ? null : itemProtoArray[index];
            if (item == null)
                mouseItemIndex = -1;
            if (selectedItem != null)
            {
                recipeSelImage.rectTransform.anchoredPosition = new Vector2(index % colCount * kGridSize - 1, -(index / colCount) * kGridSize + 1);
                recipeSelImage.gameObject.SetActive(true);
            }
            else
            {
                recipeSelImage.rectTransform.anchoredPosition = new Vector2(-1f, 1f);
                recipeSelImage.gameObject.SetActive(false);
            }

            requestAmountChanged = false;

            if (!notify)
                return;
            OnSelectedItemChange(item != selectedItem);
        }

        public void SetSelectedItem(ItemProto item, bool notify)
        {
            if (!GameMain.history.ItemUnlocked(item.ID))
                return;
            int type = item.GridIndex / 1000;
            int num1 = (item.GridIndex - type * 1000) / 100 - 1;
            int num2 = item.GridIndex % 100 - 1;
            bool unknownTypeId = !(type != 1 && type != 2);
            if (num1 < 0 || num2 < 0 || num1 >= recipeRowCount || num2 >= colCount)
                unknownTypeId = false;
            int index = num1 * colCount + num2;
            if (index < 0 || index >= itemIndexArray.Length)
                unknownTypeId = false;
            if (unknownTypeId)
            {
                OnTypeButtonClick(type);
                SetSelectedItemIndex(index, notify);
            }
            else
                SetSelectedItemIndex(-1, notify);
        }

        private void OnSelectedItemChange(bool changed)
        {
            if (selectedItem == null)
            {
                selectedItemRequestSummary.gameObject.SetActive(false);
                selectedItemCurrentState.gameObject.SetActive(false);
                selectedItemRequestSummary.transform.parent.gameObject.SetActive(false);
                selectItemIcon.gameObject.SetActive(false);
                requestAmountChanged = false;
            }
            else
            {
                var inventoryManager = PlogPlayerRegistry.LocalPlayer().inventoryManager;
                if (inventoryManager == null)
                {
                    Log.Debug($"Can't update to new item, inv mgr is null {selectedItem.Name.Translate()}");
                    return;
                }

                selectItemIcon.sprite = selectedItem.iconSprite;
                selectItemIcon.gameObject.SetActive(true);
                selectedItemRequestSummary.gameObject.SetActive(true);

                var maxDesiredAmount = 0;
                var minDesiredAmount = 0;
                var desiredItem = inventoryManager.GetDesiredItem(selectedItem.ID);

                currentRequestMin = desiredItem.RequestedStacks();
                multiValueText.text = $"{currentRequestMin}";
                currentRequestMax = desiredItem.RecycleMaxStacks();
                currentRequestMax = Math.Min(GameMain.mainPlayer.package.size, currentRequestMax);
                if (currentRequestMax != GameMain.mainPlayer.package.size)
                    multiValueMaxText.text = $"{currentRequestMax}";
                else
                {
                    multiValueMaxText.text = "Inf";
                }

                UpdateSummaryText();
                UpdateCurrentText(desiredItem);
                selectedItemRequestSummary.gameObject.SetActive(false);
                selectedItemCurrentState.gameObject.SetActive(true);
                selectedItemRequestSummary.transform.parent.gameObject.SetActive(true);
                requestAmountChanged = false;
            }
        }

        private void OnItemMouseDown(BaseEventData evtData)
        {
            if (mouseItemIndex < 0)
                return;
            if ((uint)mouseItemIndex < itemProtoArray.Length)
            {
                selectedItem = itemProtoArray[mouseItemIndex];
                if (selectedItem != null)
                    VFAudio.Create("ui-click-0", null, Vector3.zero, true);
            }

            SetSelectedItemIndex(mouseItemIndex, true);
        }

        private void OnItemMouseEnter(BaseEventData evtData)
        {
            mouseInItemAreas = true;
        }

        private void OnItemMouseExit(BaseEventData evtData)
        {
            mouseInItemAreas = false;
            mouseItemIndex = -1;
        }

        public void OnTechUnlocked(int arg0, int arg1) => RefreshItemIcons();

        public void ToggleLegacyRequestWindow()
        {
            RequestWindow.Visible = !RequestWindow.Visible;
        }

        private void CreateGridGraphic(int index)
        {
            if (numTexts[index] == null)
            {
                numTexts[index] = Instantiate(prefabNumText, itemBg.transform);
                numTexts[index].gameObject.SetActive(true);
            }
            else
            {
                numTexts[index].gameObject.SetActive(true);
            }

            if (maxTexts[index] == null)
            {
                maxTexts[index] = Instantiate(prefabNumRecycleText, itemBg.transform);
                maxTexts[index].gameObject.SetActive(true);
            }
            else
            {
                maxTexts[index].gameObject.SetActive(true);
            }

            RepositionGridGraphic(index);
        }

        private void RepositionGridGraphic(int index)
        {
            int colNum = index % colCount;
            int rowNum = index / colCount;
            numTexts[index].rectTransform.anchoredPosition = new Vector2(colNum * kGridSize - 6, rowNum * -kGridSize - 30);
            maxTexts[index].rectTransform.anchoredPosition = new Vector2(colNum * kGridSize - 6, rowNum * -kGridSize - 30);
        }
    }
}