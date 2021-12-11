using PersonalLogistics.Util;
using UnityEngine;

namespace PersonalLogistics.Scripts
{
    public class RequesterWindow : ManualBehaviour
    {
        private bool _closeRequested;
        private GameObject _instanceGo;
        private bool _openRequested;

        private uint[] iconIndexArray;

        private ComputeBuffer iconIndexBuffer;

        private UIItemRequestWindow uiItemRequestWindow;
        public static RequesterWindow Instance;

        private void Awake()
        {
            Instance = this;
        }

        private void Update()
        {
            if (PluginConfig.inventoryManagementPaused.Value)
                return;

            if (_instanceGo == null)
            {
                var prefab = LoadFromFile.LoadPrefab<GameObject>("pui", "Assets/prefab/Request Window.prefab");
                Log.Debug($"Instantiating Requester window -- prefab == null {prefab == null}");
                var uiGameInventory = UIRoot.instance.uiGame.inventory;
                _instanceGo = Instantiate(prefab, uiGameInventory.transform.parent, false);
                uiItemRequestWindow = _instanceGo.GetComponent<UIItemRequestWindow>();
                if (!uiItemRequestWindow.created && !uiItemRequestWindow.destroyed)
                    uiItemRequestWindow._Create();
                else
                    uiItemRequestWindow._OnCreate();
                if (uiItemRequestWindow.created && !uiItemRequestWindow.inited)
                    uiItemRequestWindow._Init(GameMain.mainPlayer);
                else
                {
                    uiItemRequestWindow._OnInit();
                    uiItemRequestWindow._OnRegEvent();
                }

                uiItemRequestWindow.RefreshItemIcons();

                if (uiItemRequestWindow.inited && !uiItemRequestWindow.active)
                    uiItemRequestWindow._Open();
                else
                {
                    Log.Debug($"had to use _OnOpen");
                    uiItemRequestWindow._OnOpen();
                }

                if (!uiItemRequestWindow.active)
                    uiItemRequestWindow.active = true;
                else
                {
                    Log.Debug("did not need to uiItemRequestWindow.active");
                }

                _instanceGo.SetActive(false);
            }

            if (_instanceGo.activeSelf)
            {
                uiItemRequestWindow._OnUpdate();
            }
        }


        public void Unload()
        {
            if (_instanceGo != null)
            {
                Destroy(_instanceGo);
            }
        }

        public void Toggle()
        {
            if (uiItemRequestWindow == null)
            {
                Log.Debug($"window not instantiated");
                return;
            }

            if (uiItemRequestWindow.gameObject.activeSelf)
            {
                Log.Debug($"closing request window");
                uiItemRequestWindow._Close();
            }
            else
            {
                Log.Debug($"opening request window");
                uiItemRequestWindow._Open();
                // uiItemRequestWindow.gameObject.SetActive(true);
                // uiItemRequestWindow.active = true;
            }
        }

        public void Hide()
        {
            if (uiItemRequestWindow == null)
                return;
            uiItemRequestWindow._Close();
        }
    }
}