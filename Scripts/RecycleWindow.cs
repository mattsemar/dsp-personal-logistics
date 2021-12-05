using CommonAPI.Systems;
using HarmonyLib;
using PersonalLogistics.Util;
using UnityEngine;

namespace PersonalLogistics.Scripts
{
    public class RecycleWindow : ManualBehaviour
    {
        private static RecycleWindow _instance;
        private static readonly int buffer = Shader.PropertyToID("_StateBuffer");
        private static readonly int indexBuffer = Shader.PropertyToID("_IndexBuffer");
        private bool _closeRequested;
        private GameObject _instanceGo;
        private bool _openRequested;
        private StorageComponent _storageComponent;

        private uint[] iconIndexArray;
        private ComputeBuffer iconIndexBuffer;
        private uint[] stateArray;
        private ComputeBuffer stateBuffer;
        private UIStorageGrid uiStorageGrid;

        private void Awake()
        {
            _instance = this;
        }

        private void Update()
        {
            if (_openRequested && PluginConfig.showRecycleWindow.Value)
            {
                _openRequested = false;
                if (_instanceGo == null)
                {
                    Log.Debug("Instantiating Recycle window");
                    var prefab = LoadFromFile.LoadPrefab<GameObject>("pui", "Assets/Prefab/Player Inventory Recycle.prefab");
                    var inGameGo = GameObject.Find("UI Root/Overlay Canvas/In Game/Windows");
                    _storageComponent = new StorageComponent(10);
                    _instanceGo = Instantiate(prefab, inGameGo.transform);

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
                }

                _instanceGo.SetActive(true);
            }
            else if (_closeRequested)
            {
                _closeRequested = false;
                if (_instanceGo != null)
                {
                    _instanceGo.SetActive(false);
                }
            }

            // todo, make this more aware of actions we take in background
            if (_instanceGo != null && _instanceGo.activeSelf)
            {
                uiStorageGrid.OnStorageContentChanged();
            }
        }

        private void UpdateMaterials()
        {
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


        public void Unload()
        {
            LoadFromFile.UnloadAssetBundle("pui");
            if (_instanceGo != null)
            {
                Destroy(_instanceGo);
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

            if (_instance._storageComponent == null)
            {
                return null;
            }

            return _instance._storageComponent;
        }
    }
}