using CommonAPI.Systems;
using HarmonyLib;
using PersonalLogistics.ModPlayer;
using PersonalLogistics.Util;
using UnityEngine;

namespace PersonalLogistics.Scripts
{
    public class RequesterWindow : MonoBehaviour
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
            if (GameUtil.HideUiElements())
            {
                Hide();
            }
            if (PlogPlayerRegistry.LocalPlayer() == null)
                return;
            if (_instanceGo == null)
            {
                var prefab = Asset.bundle.LoadAsset<GameObject>("Assets/prefab/Request Window.prefab");
                Log.Debug($"Instantiating Requester window -- prefab == null {prefab == null}");
                var uiGameInventory = UIRoot.instance.uiGame.inventoryWindow;
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
                    Log.Debug($"manually calling _OnInit/_OnRegEvent");
                    uiItemRequestWindow._OnInit();
                    uiItemRequestWindow._OnRegEvent();
                }

                if (uiItemRequestWindow.inited && uiItemRequestWindow.active)
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

                _instanceGo.SetActive(true);
                uiItemRequestWindow._Close();
            }

            if (_instanceGo.activeSelf)
            {
                uiItemRequestWindow._OnUpdate();
            }
            if (CustomKeyBindSystem.GetKeyBind("ShowPlogWindow").keyValue)
            {
                Toggle();
            }
        }


        public void Unload()
        {
            if (_instanceGo != null)
            {
                if (uiItemRequestWindow != null && uiItemRequestWindow.gameObject != null)
                {
                    Destroy(uiItemRequestWindow.gameObject);
                    uiItemRequestWindow = null;
                }
                Destroy(_instanceGo);
                _instanceGo = null;
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
            }
        }

        public void Hide()
        {
            if (uiItemRequestWindow == null)
                return;
            uiItemRequestWindow._Close();
        }
        
        [HarmonyPostfix]
        [HarmonyPatch(typeof(UIGame), "_OnFree")]
        public static void UIGame__OnFree_Postfix(UIGame __instance)
        {
            if (Instance != null && Instance.uiItemRequestWindow != null && Instance.uiItemRequestWindow.gameObject != null)
            {
                Log.Debug($"called req window _Free");
                Instance.uiItemRequestWindow._Free();
                Instance.Unload();
            }
            else
            {
                Log.Debug($"Taking no action for ui game free instance null: {Instance = null}");
            }
        }
        [HarmonyPostfix]
        [HarmonyPatch(typeof(UIGame), "get_isAnyFunctionWindowActive")]
        public static void UIGame_isAnyFunctionWindowActive_Postfix(ref bool __result)
        {
            if (Instance == null || Instance.uiItemRequestWindow == null || Instance.uiItemRequestWindow.gameObject == null)
            {
                return;
            }

            __result = __result || Instance.uiItemRequestWindow.gameObject.activeSelf;
        }
        
        [HarmonyPostfix]
        [HarmonyPatch(typeof(UIGame), nameof(UIGame.ShutAllFunctionWindow))]
        public static void UIGame_ShutAllFunctionWindow_Postfix()
        {
            if (Instance == null || Instance.uiItemRequestWindow == null || Instance.uiItemRequestWindow.gameObject == null)
            {
                return;
            }

            Instance.uiItemRequestWindow._Close();
        }
    }
}