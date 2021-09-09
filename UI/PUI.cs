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
            Log.Debug($"adding button based on parent {rectTransform.anchoredPosition}");
            var copied = UnityEngine.Object.Instantiate(rectTransform, parent.transform, false);
            var copiedRectTransform = copied.GetComponent<RectTransform>();
            var originalRectTransform = rectTransform.GetComponent<RectTransform>();
            // copiedRectTransform.SetParent(rectTransform.transform.parent);

            copiedRectTransform.anchorMin = rectTransform.anchorMin;
            copiedRectTransform.anchorMax = rectTransform.anchorMax;
            copiedRectTransform.sizeDelta = rectTransform.sizeDelta * 0.80f;
            copiedRectTransform.anchoredPosition = rectTransform.anchoredPosition  + positionDelta;

            var copiedCircle = copiedRectTransform.transform.Find("circle");
            if (copiedCircle != null)
            {
                _gameObjectsToDestroy.Add(copiedCircle.gameObject);
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
                Log.Debug($"Did not find icon in Child {copiedRectTransform.transform}");
            }

            var mainActionButton = copiedRectTransform.GetComponentInChildren<UIButton>();
            if (mainActionButton != null)
            {
                originalRectTransform.GetComponentInChildren<UIButton>();
                mainActionButton.tips.tipTitle = "Manage inventory";
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