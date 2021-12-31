using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using PersonalLogistics.Model;
using PersonalLogistics.UI;
using PersonalLogistics.Util;
using UnityEngine;
using UnityEngine.UI;

namespace PersonalLogistics.Scripts
{
    public class TimeScript : MonoBehaviour
    {
        private static int yOffset = 0;
        private static int xOffset = 0;
        private RectTransform _arrivalTimeRT;
        private Text _incomingText;
        private Outline _incomingTextOutline;
        private Shadow _incomingTextShadow;
        private Rect _rect;
        private int _savedHeight;
        private int _savedWidth;
        private Texture2D back;
        private GUIStyle fontSize;
        private bool loggedException;
        private GUIStyle style;
        private StringBuilder timeText;
        private GameObject txtGO;
        private Language _testLanguageOverride = Localization.language;
        private static readonly Dictionary<string, DateTime> _lastFailureMessageTime = new Dictionary<string, DateTime>();
        private bool _loadFailureReadmeReferenceMentioned;

        private void Awake()
        {
            InitText();
            StartCoroutine(Loop());
        }

        private void Update()
        {
            if (timeText == null || timeText.Length == 0)
            {
                _incomingText.text = "";
                return;
            }

            var uiGame = UIRoot.instance.uiGame;
            if (uiGame == null)
            {
                _incomingText.text = "";
                return;
            }

            if (uiGame.starmap.active || uiGame.dysonmap.active || uiGame.globemap.active || uiGame.escMenu.active || uiGame.techTree.active)
            {
                _incomingText.text = "";
                return;
            }

            if (Time.frameCount % 105 == 0)
            {
                AdjustLocation();
                if (PluginConfig.testOverrideLanguage.Value != "" && _testLanguageOverride.ToString() != PluginConfig.testOverrideLanguage.Value)
                {
                    if (Enum.TryParse(PluginConfig.testOverrideLanguage.Value, true, out Language newLang))
                    {
                        _testLanguageOverride = newLang;
                    }
                }
            }

            var text = timeText == null ? "" : timeText.ToString();
            _incomingText.text = text;
        }

        private void UpdateIncomingItems()
        {
            try
            {
                var itemLoadStates = ItemLoadState.GetLoadState(PluginConfig.timeScriptPositionTestEnabled.Value);
                if (itemLoadStates == null)
                {
                    return;
                }

                if (itemLoadStates.Count > 0)
                {
                    timeText = new StringBuilder();
                    foreach (var loadState in itemLoadStates)
                    {
                        try
                        {
                            var etaStr = FormatLoadingStatusMessage(loadState);
                            if (!string.IsNullOrWhiteSpace(etaStr))
                                timeText.Append($"{etaStr}\r\n");
                        }
                        catch (Exception e)
                        {
                            Log.Warn($"Messed up placeholders in translation. {e.Message}");
                            timeText.Append($"{loadState}\r\n");
                        }
                    }
                }
                else
                {
                    timeText = null;
                }
            }
            catch (Exception e)
            {
                if (!loggedException)
                {
                    loggedException = true;
                    Log.Warn($"failure while updating incoming items {e.Message} {e.StackTrace}");
                }
            }
        }

        private string FormatLoadingStatusMessage(ItemLoadState loadState)
        {
            switch (loadState.requestState)
            {
                case RequestState.InventoryUpdated:
                case RequestState.Complete:
                {
                    Log.Warn($"TimeScript request state is invalid {loadState.requestState}");
                    return null;
                }
                case RequestState.Created:
                {   
                    return string.Format("PLOGTaskCreated".Translate(_testLanguageOverride), loadState.itemName);
                }
                case RequestState.Failed:
                {
                    if (PluginConfig.hideIncomingItemFailures.Value)
                    {
                        return null;
                    }
                    var result = string.Format("PLOGTaskFailed".Translate(_testLanguageOverride), loadState.itemName);
                    if (!_lastFailureMessageTime.TryGetValue(result, out var lastTime))
                    {
                        lastTime = DateTime.Now.Subtract(TimeSpan.FromDays(1));
                    }
                    if ((DateTime.Now - lastTime).TotalMinutes < 5)
                    {
                        Log.Debug($"suppressing incoming item failure message: {result}");
                        return null;
                    }

                    _lastFailureMessageTime[result] = DateTime.Now;
                    if (!_loadFailureReadmeReferenceMentioned)
                    {
                        _loadFailureReadmeReferenceMentioned = true;
                        return $"{result} (see readme for more info about what this message means)";
                    }

                    return result;
                }
                case RequestState.ReadyForInventoryUpdate:
                {
                    return string.Format("PLOGLoadingFromBuffer".Translate(_testLanguageOverride), loadState.itemName);
                }
                case RequestState.WaitingForShipping:
                {
                    var etaStr = TimeUtil.FormatEta(loadState.secondsRemaining);
                    return string.Format("PLOGTaskWaitingForShipping".Translate(_testLanguageOverride), loadState.itemName, loadState.count, etaStr);
                }
                default:
                {
                    Log.Warn($"Unexpected state: {loadState.requestState} in timescript");
                    return null;
                }
            }
        }

        private IEnumerator Loop()
        {
            while (true)
            {
                yield return new WaitForSeconds(2);
                if (!PluginConfig.IsPaused() && PluginConfig.showIncomingItemProgress.Value)
                {
                    UpdateIncomingItems();
                }
                else
                {
                    timeText = null;
                }
            }
        }

        public void Unload()
        {
            try
            {
                if (_incomingText != null)
                {
                    Destroy(_incomingText.gameObject);
                    Destroy(_incomingTextOutline.gameObject);
                    Destroy(_incomingTextShadow.gameObject);
                }
            }
            catch (Exception ignored)
            {
                // ignored
            }
        }

        private void InitText()
        {
            txtGO = new GameObject("arrivalTimeText");
            _arrivalTimeRT = txtGO.AddComponent<RectTransform>();

            var inGameGo = GameObject.Find("UI Root/Overlay Canvas/In Game");
            _arrivalTimeRT.anchorMax = new Vector2(0, 0.5f);
            _arrivalTimeRT.anchorMin = new Vector2(0, 0.5f);
            _arrivalTimeRT.sizeDelta = new Vector2(100, 20);
            _arrivalTimeRT.pivot = new Vector2(0, 0.5f);
            _incomingText = _arrivalTimeRT.gameObject.AddComponent<Text>();

            _incomingTextOutline = _incomingText.gameObject.AddComponent<Outline>();
            _incomingTextOutline.effectDistance = new Vector2(1, 1);
            _incomingTextOutline.effectColor = new Color(0f, 0f, 0f, 0.5f);

            _incomingTextShadow = _incomingText.gameObject.AddComponent<Shadow>();
            _incomingTextShadow.effectColor = new Color(0f, 0f, 0f, 0.6706f);
            _incomingTextShadow.effectDistance = new Vector2(2, -1);

            _incomingText.text = "Hello operator";
            _incomingText.fontStyle = FontStyle.Normal;
            _incomingText.fontSize = 20;
            _incomingText.verticalOverflow = VerticalWrapMode.Overflow;
            _incomingText.horizontalOverflow = HorizontalWrapMode.Overflow;
            _incomingText.color = new Color(1f, 1f, 1f, 1f);
            var fnt = Resources.Load<Font>("ui/fonts/SAIRASB");
            if (fnt != null)
            {
                _incomingText.font = fnt;
            }

            txtGO.transform.SetParent(inGameGo.transform, false);
            AdjustLocation();
        }

        private void AdjustLocation()
        {
            if (_savedHeight == DSPGame.globalOption.uiLayoutHeight && _savedWidth == Screen.width)
            {
                return;
            }

            if (_arrivalTimeRT == null)
            {
                return;
            }

            _savedHeight = DSPGame.globalOption.uiLayoutHeight;
            _savedWidth = Screen.width;
            _arrivalTimeRT.anchoredPosition = new Vector2(UiScaler.ScaleToDefault(25), _savedHeight / 4f);

            _incomingText.fontSize = UiScaler.ScaleToDefault(12, false);
        }
    }
}