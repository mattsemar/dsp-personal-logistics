using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PersonalLogistics.UI
{
    public static class PUI
    {
        private static List<GameObject> _gameObjectsToDestroy = new List<GameObject>();

        public static RectTransform CopyButton(RectTransform rectTransform, Vector2 positionDelta, Sprite newIcon, Action<int> action)
        {
            var parent = rectTransform.transform.parent.GetComponent<RectTransform>();
            Log.Debug($"adding button based on parent {rectTransform.anchoredPosition} {rectTransform.sizeDelta}");
            var copied = UnityEngine.Object.Instantiate(rectTransform, parent.transform, false);
            var copiedRectTransform = copied.GetComponent<RectTransform>();
            var originalRectTransform = rectTransform.GetComponent<RectTransform>();
            // use for tweaking position
            // if (Math.Abs(rectTransform.anchoredPosition.x - (-84)) > 0.1)
                // rectTransform.anchoredPosition = new Vector2(-84, 0);
            
            rectTransform.sizeDelta = new Vector2(55f, 55f);
            rectTransform.anchoredPosition = new Vector2(rectTransform.anchoredPosition.x + 10, rectTransform.anchoredPosition.y - 10);
            copiedRectTransform.anchorMin = rectTransform.anchorMin;
            copiedRectTransform.anchorMax = rectTransform.anchorMax;
            copiedRectTransform.sizeDelta = rectTransform.sizeDelta * 0.1f;
            copiedRectTransform.anchoredPosition = rectTransform.anchoredPosition  + positionDelta;

            var copiedCircle = copiedRectTransform.transform.Find("circle");
            if (copiedCircle != null)
            {
                _gameObjectsToDestroy.Add(copiedCircle.gameObject);
                var cirRt = copiedCircle.GetComponent<RectTransform>();
                cirRt.anchorMin = new Vector2(0, 1);
                cirRt.anchorMax = new Vector2(0, 1);
                cirRt.sizeDelta = new Vector2(40, 40);
            }
            else
            {
                Log.Warn($"did not find copied circle");
            }


            
            var copiedIconTrans = copiedRectTransform.transform.FindChildRecur("icon");
            if (copiedIconTrans != null)
            {
                _gameObjectsToDestroy.Add(copiedIconTrans.gameObject);
                var copiedImage = copiedIconTrans.GetComponentInChildren<Image>();
                if (copiedImage != null)
                {
                    copiedImage.sprite = newIcon;
                    _gameObjectsToDestroy.Add(copiedImage.gameObject);
                }
            }
            else
            {
                Log.Debug($"Did not find icon in Child {copied.transform}");
            }

            var mainActionButton = copiedRectTransform.GetComponentInChildren<UIButton>();
            if (mainActionButton != null)
            {
                originalRectTransform.GetComponentInChildren<UIButton>();
                mainActionButton.tips.tipTitle = "Manage inventory (Ctrl+E)";
                mainActionButton.tips.tipText = "Set desired item counts for inventory";
                mainActionButton.tips.offset = new Vector2(mainActionButton.tips.offset.x, mainActionButton.tips.offset.y + 100);
                mainActionButton.button.onClick.RemoveAllListeners();

                // mainActionButton.onClick += action;
                mainActionButton.button.onClick.AddListener(delegate { action(1); });
            }
            else
            {
                Log.Warn($"did not find main action");
            }


            return copied;
        }
        
        // private void InitUiButton(RectTransform gameMenuContainer)
        // {
        //     var configBtn = new GameObject("Config");
        //     _gameObjectsToDestroy.Add(configBtn);
        //     var rect = configBtn.AddComponent<RectTransform>();
        //     rect.SetParent(gameMenuContainer.transform, false);
        //
        //     rect.anchorMax = new Vector2(0, 1);
        //     rect.anchorMin = new Vector2(0, 1);
        //     rect.sizeDelta = new Vector2(20, 20);
        //     rect.pivot = new Vector2(0, 0.5f);
        //     rect.anchoredPosition = new Vector2(375, -120);
        //     var invokeConfig = rect.gameObject.AddComponent<CheckboxControl>();
        //     invokeConfig.HoverText = "Open config";
        //
        //     if (countText != null)
        //     {
        //         var configHover = Instantiate(countText, gameMenuContainer.transform, true);
        //         gameObjectsToDestroy.Add(configHover.gameObject);
        //         var copiedRectTransform = configHover.GetComponent<RectTransform>();
        //         var parentRect = gameMenuContainer.GetComponent<RectTransform>();
        //         copiedRectTransform.anchorMin = new Vector2(0, 1);
        //         copiedRectTransform.anchorMax = new Vector2(0, 1);
        //         copiedRectTransform.sizeDelta = new Vector2(800, 20);
        //         copiedRectTransform.anchoredPosition = new Vector2(400, parentRect.transform.position.y - 115);
        //         invokeConfig.textObject = configHover;
        //     }
        //
        //     gameObjectsToDestroy.Add(invokeConfig.gameObject);
        //
        //     ConfigIconImage = invokeConfig.gameObject.AddComponent<Image>();
        //     ConfigIconImage.color = new Color(0.8f, 0.8f, 0.8f, 1);
        //     gameObjectsToDestroy.Add(ConfigIconImage.gameObject);
        //     var configImgGameObject = GameObject.Find("UI Root/Overlay Canvas/In Game/Game Menu/button-3-bg/button-3/icon");
        //
        //     ConfigIconImage.sprite = configImgGameObject.GetComponent<Image>().sprite;
        //     invokeConfig.onClick += data => { PluginConfigWindow.visible = !PluginConfigWindow.visible; };
        // }

        public static void Unload()
        {
            try
            {
                while (_gameObjectsToDestroy.Count > 0)
                {
                    var gameObject = _gameObjectsToDestroy[0];
                    if (gameObject != null)
                        UnityEngine.Object.Destroy(gameObject);
                    _gameObjectsToDestroy.RemoveAt(0);
                }
            }
            catch (Exception e)
            {
                Log.Warn($"failed to do unload {e.Message}");
                Log.Warn(e.StackTrace);
            }
        }
    }
}