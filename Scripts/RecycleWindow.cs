using System;
using CommonAPI.Systems;
using HarmonyLib;
using PersonalLogistics.Logistics;
using PersonalLogistics.Model;
using PersonalLogistics.Util;
using UnityEngine;
using UnityEngine.UI;

namespace PersonalLogistics.Scripts
{
    public class RecycleWindow : ManualBehaviour
    {
        private static RecycleWindow _instance;
        private static readonly int buffer = Shader.PropertyToID("_StateBuffer");
        private static readonly int indexBuffer = Shader.PropertyToID("_IndexBuffer");
        private static Texture2D texOff = Resources.Load<Texture2D>("ui/textures/sprites/icons/checkbox-off");
        private static Texture2D texOn = Resources.Load<Texture2D>("ui/textures/sprites/icons/checkbox-on");
        private readonly DelayedContainer<GridItem> _recycledItems = new DelayedContainer<GridItem>(TimeSpan.FromSeconds(PluginConfig.minRecycleDelayInSeconds.Value));

        private bool _closeRequested;
        private GameObject _instanceGo;
        private bool _openRequested;
        private StorageComponent _storageComponent;

        private uint[] iconIndexArray;
        private ComputeBuffer iconIndexBuffer;
        private uint[] stateArray;
        private ComputeBuffer stateBuffer;
        private UIStorageGrid uiStorageGrid;
        private Sprite sprOn;
        private Sprite sprOff;
        private GameObject txtGO;
        private GameObject chxGO;
        private Image checkBoxImage;

        private void Awake()
        {
            _instance = this;
            PluginConfig.minRecycleDelayInSeconds.SettingChanged += RecycleTimeConfigPropertyChanged;
        }

        private void RecycleTimeConfigPropertyChanged(object sender, EventArgs e)
        {
            var delayValue = PluginConfig.minRecycleDelayInSeconds.Value;
            Log.Debug($"Got update event for delay property. New value: {delayValue}. Old value {_recycledItems.MinAgeSeconds()}");
            if (_recycledItems.MinAgeSeconds() != delayValue)
            {
                Log.Debug($"updating delay property");
                _recycledItems.UpdateMinAgeSeconds(delayValue);
            }
        }

        private void Update()
        {
            if (_instanceGo != null && _instanceGo.activeSelf)
            {
                uiStorageGrid.OnStorageContentChanged();
            }

            if (PluginConfig.inventoryManagementPaused.Value)
                return;

            // remove recycle window as target for shift clicking logistics vessels/bots when another station window is open
            if (UIRoot.instance.uiGame.stationWindow != null && UIRoot.instance.uiGame.stationWindow.gameObject.activeSelf && uiStorageGrid != null)
            {
                UIStorageGrid.openedStorages.Remove(uiStorageGrid);
            }

            // remove recycle window as target for shift clicking if another storage window was opened (player inv open and then storage window is opened)
            if (uiStorageGrid != null && UIStorageGrid.openedStorages.Contains(uiStorageGrid) && UIStorageGrid.openedStorages.Count > 2)
            {
                UIStorageGrid.openedStorages.Remove(uiStorageGrid);
            }

            if (_instanceGo != null && !_instanceGo.activeSelf && uiStorageGrid != null)
                UIStorageGrid.openedStorages.Remove(uiStorageGrid);

            if (_openRequested && PluginConfig.showRecycleWindow.Value)
            {
                _openRequested = false;
                if (_instanceGo == null)
                {
                    AddShowRecycleCheck();

                    Log.Debug("Instantiating Recycle window");
                    var prefab = LoadFromFile.LoadPrefab<GameObject>("pui", "Assets/Prefab/Player Inventory Recycle.prefab");
                    var uiGameInventory = UIRoot.instance.uiGame.inventory;
                    _storageComponent = new StorageComponent(10);
                    _instanceGo = Instantiate(prefab, uiGameInventory.transform, false);

                    uiStorageGrid = _instanceGo.GetComponentInChildren<UIStorageGrid>();
                    uiStorageGrid._OnCreate();
                    uiStorageGrid.data = _storageComponent;

                    uiStorageGrid.storage = _storageComponent;
                    uiStorageGrid.rowCount = 1;
                    uiStorageGrid.colCount = 10;
                    uiStorageGrid._OnInit();

                    UpdateMaterials();

                    uiStorageGrid.storage = _storageComponent;
                    uiStorageGrid.OnStorageDataChanged();
                    uiStorageGrid.storage.onStorageChange += RecordStorageChange;
                    var tipTexGo = GameObject.Find("UI Root/Overlay Canvas/In Game/Windows/Player Inventory/panel-bg/tip-text");
                    float yOffset = GetYOffset();
                    uiStorageGrid.rectTrans.position =
                        new Vector3(uiStorageGrid.rectTrans.transform.position.x, tipTexGo.transform.position.y - yOffset, tipTexGo.transform.position.z);
                    var panel = GameObject.Find("UI Root/Overlay Canvas/In Game/Windows/Player Inventory/Player Inventory Recycle(Clone)/panel-bg");
                    if (panel != null)
                    {
                        panel.transform.localScale = new Vector3(panel.transform.localScale.x * 0.95f, panel.transform.localScale.y, panel.transform.localScale.z);
                    }
                }

                // Add the recycle storage grid to the list of opened storages so items can be shift-clicked into it. Only do this if another storage is not open since
                // the preference should be to move items into an open storage bin over recycling
                if (UIStorageGrid.openedStorages.Count == 1 && (UIRoot.instance.uiGame.stationWindow == null || !UIRoot.instance.uiGame.stationWindow.gameObject.activeSelf))
                    UIStorageGrid.openedStorages.Add(uiStorageGrid);
            }
            else if (_closeRequested)
            {
                _closeRequested = false;
                if (_instanceGo != null)
                {
                    // _instanceGo.SetActive(false);
                }

                if (uiStorageGrid != null)
                {
                    UIStorageGrid.openedStorages.Remove(uiStorageGrid);
                }
            }
        }

        private void RecordStorageChange()
        {
            if (uiStorageGrid == null || uiStorageGrid.storage == null)
            {
                // not sure how this would happen
                Log.Warn("Storage component notified of change but null reference found");
                return;
            }

            var itemsToRecycle = uiStorageGrid.storage;
            for (var index = 0; index < itemsToRecycle.size; ++index)
            {
                var itemId = itemsToRecycle.grids[index].itemId;
                if (itemId == 0)
                {
                    continue;
                }

                var count = itemsToRecycle.grids[index].count;
                if (count < 1)
                {
                    continue;
                }

                var gridItem = GridItem.From(index, itemId, count);
                if (_recycledItems.HasItem(gridItem))
                {
                    continue;
                }

                if (!LogisticsNetwork.HasItem(itemId))
                {
                    var removedCount = itemsToRecycle.TakeItem(itemId, count);
                    GameMain.mainPlayer.TryAddItemToPackage(itemId, count, true);
                    continue;
                }

                _recycledItems.AddItems(gridItem);
            }
        }

        private float GetYOffset()
        {
            // 1.15f seems to work for 1080
            if (DSPGame.globalOption.uiLayoutHeight == 1080)
                return 1.15f;
            var multiplier = DSPGame.globalOption.uiLayoutHeight / 1080f;
            return 1.15f / multiplier;
        }

        private void AddShowRecycleCheck()
        {
            sprOn = Sprite.Create(texOn, new Rect(0, 0, texOn.width, texOn.height), new Vector2(0.5f, 0.5f));
            sprOff = Sprite.Create(texOff, new Rect(0, 0, texOff.width, texOff.height), new Vector2(0.5f, 0.5f));
            // first shrink down inventory label and move up slightly
            var titleTextGo = GameObject.Find("UI Root/Overlay Canvas/In Game/Windows/Player Inventory/panel-bg/title-text");
            var titleText = titleTextGo.transform.GetComponent<Text>();
            titleText.fontSize = 14;
            var titleTextRT = titleTextGo.transform.GetComponent<RectTransform>();
            titleTextRT.anchoredPosition = new Vector2(titleTextRT.anchoredPosition.x, -8);

            // add checkbox
            chxGO = new GameObject("displayRecycleWindowCheck");

            RectTransform checkBoxRectTransform = chxGO.AddComponent<RectTransform>();
            checkBoxRectTransform.SetParent(UIRoot.instance.uiGame.inventory.transform, false);

            checkBoxRectTransform.anchorMax = new Vector2(0, 1);
            checkBoxRectTransform.anchorMin = new Vector2(0, 1);
            checkBoxRectTransform.sizeDelta = new Vector2(15, 15);
            checkBoxRectTransform.pivot = new Vector2(0, 0.5f);
            checkBoxRectTransform.anchoredPosition = new Vector2(6, titleTextRT.anchoredPosition.y + 20);

            Button _btn = checkBoxRectTransform.gameObject.AddComponent<Button>();
            _btn.onClick.AddListener(() =>
            {
                if (_instanceGo != null)
                {
                    _instanceGo.SetActive(!_instanceGo.activeSelf);
                    checkBoxImage.sprite = _instanceGo.activeSelf ? sprOn : sprOff;
                }
            });
            checkBoxImage = _btn.gameObject.AddComponent<Image>();
            checkBoxImage.color = new Color(0.8f, 0.8f, 0.8f, 1);
            checkBoxImage.sprite = sprOn;

            txtGO = new GameObject("displayRecycleWindowCheckText");
            var textRectTransform = txtGO.AddComponent<RectTransform>();

            textRectTransform.SetParent(chxGO.transform, false);

            textRectTransform.anchorMax = new Vector2(0, 1f);
            textRectTransform.anchorMin = new Vector2(0, 1f);
            textRectTransform.sizeDelta = new Vector2(100, 15);
            textRectTransform.pivot = new Vector2(0, 0.5f);
            textRectTransform.anchoredPosition = new Vector2(20, -5);

            Text text = textRectTransform.gameObject.AddComponent<Text>();
            text.text = "Show recycle section";
            text.fontStyle = FontStyle.Normal;
            text.fontSize = 11;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.color = new Color(0.8f, 0.8f, 0.8f, 1);
            Font fnt = Resources.Load<Font>("ui/fonts/SAIRASB");
            if (fnt != null)
                text.font = fnt;
        }

        private void UpdateMaterials()
        {
            uiStorageGrid.iconImage.texture = GameMain.iconSet.texture;
            if (stateArray == null)
            {
                stateArray = uiStorageGrid.stateArray ?? new uint[1024];

                stateBuffer = uiStorageGrid.stateBuffer ?? new ComputeBuffer(stateArray.Length, 4);

                iconIndexArray = uiStorageGrid.iconIndexArray ?? new uint[1024];

                iconIndexBuffer = uiStorageGrid.iconIndexBuffer ?? new ComputeBuffer(iconIndexArray.Length, 4);
            }

            var bgImage = uiStorageGrid.bgImage;
            if (bgImage != null)
            {
                uiStorageGrid.bgImageMat = ProtoRegistry.CreateMaterial("UI Ex/Storage Bg", "storage-bg", "#FFFFFFFF", null, new string[] { });
                uiStorageGrid.bgImageMat.SetBuffer(buffer, stateBuffer);
                bgImage.material = uiStorageGrid.bgImageMat;
            }

            var iconImage = uiStorageGrid.iconImage;
            if (iconImage != null)
            {
                uiStorageGrid.iconImageMat = ProtoRegistry.CreateMaterial("UI Ex/Storage Icons", "storage-icons", "#FFFFFFFF", null, new string[] { });
                uiStorageGrid.iconImageMat.SetBuffer(indexBuffer, iconIndexBuffer);
                iconImage.material = uiStorageGrid.iconImageMat;
            }
        }


        public void Unload(bool unloadAssetBundle)
        {
            if (unloadAssetBundle)
                LoadFromFile.UnloadAssetBundle("pui");
            if (uiStorageGrid != null)
            {
                uiStorageGrid.storage.onStorageChange -= RecordStorageChange;
                uiStorageGrid.bgImageMat = null;
                uiStorageGrid.bgImage = null;
                Destroy(uiStorageGrid.gameObject);
                uiStorageGrid = null;
            }

            if (txtGO != null)
                Destroy(txtGO);
            if (chxGO != null)
                Destroy(chxGO);
            if (_instanceGo != null)
            {
                Destroy(_instanceGo);
                _instanceGo = null;
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(UIStorageGrid), "_OnOpen")]
        public static void UIStorageGrid__OnOpen_Postfix(UIStorageGrid __instance)
        {
            if (__instance.primary && _instance != null)
            {
                _instance._openRequested = true;
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(UIStorageGrid), "_OnClose")]
        public static void UIStorageGrid__OnOClose_Postfix(UIStorageGrid __instance)
        {
            if (__instance.primary && _instance != null)
            {
                _instance._closeRequested = true;
            }
        }

        public static StorageComponent GetItemsToRecycle()
        {
            if (_instance == null)
            {
                return null;
            }

            return _instance._storageComponent;
        }

        public static GridItem GetItemToRecycle()
        {
            if (_instance == null)
            {
                return null;
            }

            return _instance.GetItemToRecycleImpl();
        }

        private GridItem GetItemToRecycleImpl()
        {
            var poppedItem = _recycledItems.PopAvailableItem();
            if (poppedItem == null)
            {
                return null;
            }

            if (poppedItem is GridItem item)
            {
                return item;
            }

            throw new Exception($"Object is not null and not a griditem? wtf: {poppedItem}");
        }

        public static void RemoveFromStorage(GridItem gridItem)
        {
            if (_instance == null)
            {
                return;
            }

            _instance.RemoveFromStorageImpl(gridItem);
        }

        private void RemoveFromStorageImpl(GridItem gridItem)
        {
            if (uiStorageGrid == null || uiStorageGrid.storage == null)
                return;
            uiStorageGrid.storage.TakeItemFromGrid(gridItem.Index, ref gridItem.ItemId, ref gridItem.Count);
        }
    }
}