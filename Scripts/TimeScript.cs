using System;
using System.Collections.Generic;
using System.Text;
using PersonalLogistics.Logistics;
using PersonalLogistics.Model;
using PersonalLogistics.Util;
using TMPro;
using UnityEngine;
using Random = UnityEngine.Random;

namespace PersonalLogistics.Scripts
{
    public class TimeScript : MonoBehaviour
    {
        public TMP_Text inboundItemStatus;
        private bool _textDirty = true;
        private string _newText;

        private bool _runOnce;
        private bool _iconsLoaded;

        // so we can change it using runtime editor
        public static Language _testLanguageOverride = Localization.language;
        private static readonly Dictionary<string, DateTime> _lastFailureMessageTime = new();
        private static readonly Dictionary<string, DateTime> _itemNameFirstShownFailureMessageTime = new();
        private int _loadFailureReadmeReferenceMentionedCountDown = 5;
        private const int _maxCharCount = 1000;

        private void Awake()
        {
            if (!_iconsLoaded)
            {
                SpriteSheetManager.Create(GameMain.iconSet, inboundItemStatus);
                _iconsLoaded = true;
            }
        }

        private void Update()
        {
            if (_textDirty)
            {
                _textDirty = false;
                inboundItemStatus.text = _newText;
            }

            if (!LogisticsNetwork.IsInitted)
                return;
            if (Time.frameCount % 60 == 0 || !_runOnce)
            {
                _runOnce = true;
                if (!PluginConfig.IsPaused() && PluginConfig.showIncomingItemProgress.Value)
                {
                    UpdateIncomingItems();
                }
                else
                {
                    inboundItemStatus.gameObject.SetActive(false);
                }
            }

            if (GameUtil.HideUiElements() || PluginConfig.IsPaused())
            {
                inboundItemStatus.gameObject.SetActive(false);
                return;
            }

            if (Time.frameCount % 105 == 0)
            {
                if (PluginConfig.testOverrideLanguage.Value != "" && _testLanguageOverride.ToString() != PluginConfig.testOverrideLanguage.Value)
                {
                    if (Enum.TryParse(PluginConfig.testOverrideLanguage.Value, true, out Language newLang))
                    {
                        _testLanguageOverride = newLang;
                    }
                }
            }

            inboundItemStatus.gameObject.SetActive(PluginConfig.showIncomingItemProgress.Value && !string.IsNullOrEmpty(inboundItemStatus.text));
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

                var newText = new StringBuilder();
                var lineCount = 0;
                foreach (var loadState in itemLoadStates)
                {
                    try
                    {
                        var etaStr = FormatLoadingStatusMessage(loadState);
                        if (!string.IsNullOrWhiteSpace(etaStr))
                        {
                            newText.Append($"{etaStr}\r\n");
                            lineCount++;
                        }
                    }
                    catch (Exception e)
                    {
                        Log.Warn($"Messed up placeholders in translation. {e.Message}");
                        newText.Append($"{loadState}\r\n");
                    }

                    if (lineCount > GetMaxLineCount())
                        break;
                }


                // if (newText.Length > _maxCharCount)
                // {
                //     _newText = newText.ToString().Substring(0, _maxCharCount - 3) + "...";
                // }
                // else
                    _newText = newText.ToString();

                _textDirty = true;
            }
            catch (Exception e)
            {
                Log.Warn($"failure while updating incoming items {e.Message} {e.StackTrace}");
            }
        }

        private int GetMaxLineCount()
        {
            // at 1920x1080 we start going into minimap after about 10 lines
            // return Math.Min(UiScaler.ScaleToDefault(10, false), 15);
            return int.MaxValue;
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
                    return string.Format("PLOGTaskCreated".Translate(_testLanguageOverride), $"<sprite name=\"{loadState.itemName}\">", loadState.count);
                }
                case RequestState.Failed:
                {
                    if (PluginConfig.hideIncomingItemFailures.Value)
                    {
                        return null;
                    }

                    var result = string.Format("PLOGTaskFailed".Translate(_testLanguageOverride), $"<sprite name=\"{loadState.itemName}\">");

                    if (!_itemNameFirstShownFailureMessageTime.TryGetValue(loadState.itemName, out var firstTime))
                        _itemNameFirstShownFailureMessageTime[result] = DateTime.Now;

                    _lastFailureMessageTime[result] = DateTime.Now + TimeSpan.FromSeconds(Random.RandomRangeInt(10, 65));
                    if (_loadFailureReadmeReferenceMentionedCountDown-- > 0)
                    {
                        return $"{result} (see readme for more about what this message means)";
                    }

                    return result;
                }
                case RequestState.ReadyForInventoryUpdate:
                {
                    _itemNameFirstShownFailureMessageTime.Remove(loadState.itemName);
                    return string.Format("PLOGLoadingFromBuffer".Translate(_testLanguageOverride), $"<sprite name=\"{loadState.itemName}\">",loadState.count);
                }
                case RequestState.WaitingForShipping:
                {
                    var shippingAmount = Math.Max(loadState.cost?.shippingToBufferCount ?? loadState.count, loadState.count);
                    _itemNameFirstShownFailureMessageTime.Remove(loadState.itemName);
                    if (loadState.cost?.paid != null && loadState.cost.paid)
                    {
                        var etaStr = TimeUtil.FormatEta(Math.Max(loadState.secondsRemaining, 0.8f));
                        return string.Format("PLOGTaskWaitingForShipping".Translate(_testLanguageOverride), $"<sprite name=\"{loadState.itemName}\">", shippingAmount, etaStr);
                    }

                    if (loadState.cost == null)
                    {
                        Log.Warn($"missed setting cost for waiting for shipping");
                        var etaStr = TimeUtil.FormatEta(loadState.secondsRemaining);
                        return string.Format("PLOGTaskWaitingForShipping".Translate(_testLanguageOverride), $"<sprite name=\"{loadState.itemName}\">", shippingAmount, etaStr);
                    }

                    var stationInfo = LogisticsNetwork.FindStation(loadState.cost.stationGid, loadState.cost.planetId, loadState.cost.stationId);
                    var planetName = stationInfo?.PlanetName ?? "Unknown";
                    var stationType = stationInfo?.StationType.ToString() ?? "Unknown";
                    if (planetName == "Unknown" || stationType == "Unknown")
                    {
                        Log.Warn($"Failed to get station info from cost: {loadState.cost.planetId}, {loadState.cost.stationId} {stationInfo}");
                    }

                    if (loadState.cost.processingPassesCompleted < 3)
                    {
                        // shipping isn't delayed (yet), it's just that the hasn't been checked
                        return string.Format("PLOGShippingCostProcessing".Translate(_testLanguageOverride), $"<sprite name=\"{loadState.itemName}\">", shippingAmount);
                    }

                    if (loadState.cost.needWarper)
                    {
                        return string.Format("PLOGShippingDelayedWarper".Translate(_testLanguageOverride), $"<sprite name=\"{loadState.itemName}\">", planetName, stationType);
                    }

                    return string.Format("PLOGShippingDelayedEnergy".Translate(_testLanguageOverride), $"<sprite name=\"{loadState.itemName}\">", planetName, stationType);
                }
                default:
                {
                    Log.Warn($"Unexpected state: {loadState.requestState} in timescript");
                    return null;
                }
            }
        }
    }
}