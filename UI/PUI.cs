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
            ResetButton(rectTransform);
            var parent = rectTransform.transform.parent.GetComponent<RectTransform>();
            Log.Debug($"adding button based on parent {rectTransform.anchoredPosition} {rectTransform.sizeDelta}");
            var copied = UnityEngine.Object.Instantiate(rectTransform, parent.transform, false);
            var copiedRectTransform = copied.GetComponent<RectTransform>();
            var originalRectTransform = rectTransform.GetComponent<RectTransform>();

            rectTransform.sizeDelta = new Vector2(50f, 50f);
            rectTransform.anchoredPosition = new Vector2(rectTransform.anchoredPosition.x + 17, rectTransform.anchoredPosition.y );
            copiedRectTransform.anchorMin = rectTransform.anchorMin;
            copiedRectTransform.anchorMax = rectTransform.anchorMax;
            copiedRectTransform.sizeDelta = rectTransform.sizeDelta * 0.09f;
            copiedRectTransform.anchoredPosition = rectTransform.anchoredPosition + positionDelta;

            var copiedCircle = copiedRectTransform.transform.Find("circle");
            if (copiedCircle != null)
            {
                _gameObjectsToDestroy.Add(copiedCircle.gameObject);
                var cirRt = copiedCircle.GetComponent<RectTransform>();
                cirRt.anchorMin = new Vector2(0, 1);
                cirRt.anchorMax = new Vector2(0, 1);
                cirRt.sizeDelta = new Vector2(38, 38);
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
                    copiedImage.type = Image.Type.Simple;
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

        private static void ResetButton(RectTransform rectTransform)
        {
            ResetButtonPos(rectTransform);
            ResetButtonSz(rectTransform);
        }

        private static void ResetButtonPos(RectTransform rectTransform)
        {
            var posStr = $"{rectTransform.anchoredPosition.x},{rectTransform.anchoredPosition.y}";
            if (PluginConfig.originalButtonPosition.Value == "0,0")
            {
                PluginConfig.originalButtonPosition.Value = posStr;
            }
            else if (posStr != PluginConfig.originalButtonPosition.Value)
            {
                var parts = PluginConfig.originalButtonPosition.Value.Split(',');
                try
                {
                    if (float.TryParse(parts[0].Trim(), out float resultx))
                    {
                        if (float.TryParse(parts[1].Trim(), out float resulty))
                        {
                            Log.Debug($"Setting button back to original {PluginConfig.originalButtonPosition.Value} {resultx}, {resulty}");
                            rectTransform.anchoredPosition = new Vector2(resultx, resulty);
                        }
                        else
                        {
                            Log.Debug($"Failed to parse yvalue {parts[1]}");
                        }
                    }
                    else
                    {
                        Log.Debug($"Failed to parse xvalue {parts[0]}");
                    }
                }
                catch (Exception e)
                {
                    // ignored
                }
            }
        }

        private static void ResetButtonSz(RectTransform rectTransform)
        {
            var szStr = $"{rectTransform.sizeDelta.x},{rectTransform.sizeDelta.y}";
            if (PluginConfig.originalButtonSz.Value == "0,0")
            {
                PluginConfig.originalButtonSz.Value = szStr;
            }
            else if (szStr != PluginConfig.originalButtonSz.Value)
            {
                var parts = PluginConfig.originalButtonSz.Value.Split(',');
                try
                {
                    if (float.TryParse(parts[0].Trim(), out float resultx))
                    {
                        if (float.TryParse(parts[1].Trim(), out float resulty))
                        {
                            Log.Debug($"Setting button sz back to original {PluginConfig.originalButtonSz.Value} {resultx}, {resulty}");
                            rectTransform.sizeDelta = new Vector2(resultx, resulty);
                        }
                        else
                        {
                            Log.Debug($"Failed to parse yvalue for sz {parts[1]}");
                        }
                    }
                    else
                    {
                        Log.Debug($"Failed to parse xvalue for sz {parts[0]}");
                    }
                }
                catch (Exception e)
                {
                    // ignored
                }
            }
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