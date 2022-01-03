using System;
using System.Collections.Generic;
using System.Text;
using PersonalLogistics.Logistics;
using PersonalLogistics.Model;
using PersonalLogistics.Util;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace PersonalLogistics.Scripts
{
    public class TimeScript : MonoBehaviour
    {
        public Text errorItems;
        public Text bufferToInventoryItems;
        public Text stationToBufferItems;

        public RectTransform textSectionsRt;

        private bool _runOnce;

        // so we can change it using runtime editor
        public static Language _testLanguageOverride = Localization.language;
        private static readonly Dictionary<string, DateTime> _lastFailureMessageTime = new Dictionary<string, DateTime>();
        private int _loadFailureReadmeReferenceMentionedCountDown = 5;

        private void Update()
        {
            if (!LogisticsNetwork.IsInitted)
                return;
            if (Time.frameCount % 61 == 0 || !_runOnce)
            {
                _runOnce = true;
                if (!PluginConfig.IsPaused() && PluginConfig.showIncomingItemProgress.Value)
                {
                    UpdateIncomingItems();
                }
                else
                {
                    textSectionsRt.gameObject.SetActive(false);
                }
            }


            var uiGame = UIRoot.instance.uiGame;
            if (uiGame == null)
            {
                textSectionsRt.gameObject.SetActive(false);
                return;
            }

            if (uiGame.starmap.active || uiGame.dysonmap.active || uiGame.globemap.active || uiGame.escMenu.active || uiGame.techTree.active)
            {
                textSectionsRt.gameObject.SetActive(false);
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

            textSectionsRt.gameObject.SetActive(true);
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

                var newErrorText = new StringBuilder();
                var newBuffToInvText = new StringBuilder();
                var newStationToBuffText = new StringBuilder();
                int addedCount = 0;
                int curIndex = 0;
                foreach (var loadState in itemLoadStates)
                {
                    try
                    {
                        var etaStr = FormatLoadingStatusMessage(loadState);
                        if (!string.IsNullOrWhiteSpace(etaStr)) {
                            StringBuilder curBuilder = newErrorText;
                        // {
                            // StringBuilder curBuilder = null;
                            // switch (loadState.requestState)
                            // {
                            //     case RequestState.Failed:
                            //         curBuilder = newErrorText;
                            //         break;
                            //     case RequestState.ReadyForInventoryUpdate:
                            //         curBuilder = newBuffToInvText;
                            //         break;
                            //     case RequestState.WaitingForShipping:
                            //         curBuilder = newStationToBuffText;
                            //         break;
                            // }

                            if (curBuilder != null)
                            {
                                curBuilder.Append($"{etaStr}\r\n");
                                addedCount++;
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Log.Warn($"Messed up placeholders in translation. {e.Message}");
                        newErrorText.Append($"{loadState}\r\n");
                    }

                    if (addedCount > 15)
                    {
                        var remaining = itemLoadStates.Count - curIndex;
                        newStationToBuffText.Append($"[{remaining} more]");
                        break;
                    }

                    curIndex++;
                }

                errorItems.text = newErrorText.ToString();
                bufferToInventoryItems.text = newBuffToInvText.ToString();
                stationToBufferItems.text = newStationToBuffText.ToString();
            }
            catch (Exception e)
            {
                Log.Warn($"failure while updating incoming items {e.Message} {e.StackTrace}");
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
                    return string.Format("PLOGTaskCreated".Translate(_testLanguageOverride), loadState.itemName, loadState.count);
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

                    if ((DateTime.Now - lastTime).TotalMinutes < 1)
                    {
                        return null;
                    }

                    _lastFailureMessageTime[result] = DateTime.Now + TimeSpan.FromSeconds(Random.RandomRangeInt(10, 65));
                    if (_loadFailureReadmeReferenceMentionedCountDown-- > 0)
                    {
                        return $"{result} (see readme for more about what this message means)";
                    }

                    return result;
                }
                case RequestState.ReadyForInventoryUpdate:
                {
                    return string.Format("PLOGLoadingFromBuffer".Translate(_testLanguageOverride), loadState.itemName, loadState.count);
                }
                case RequestState.WaitingForShipping:
                {
                    if (loadState.cost?.paid != null && (bool)loadState.cost?.paid)
                    {
                        var etaStr = TimeUtil.FormatEta(Math.Max(loadState.secondsRemaining, 0f));
                        return string.Format("PLOGTaskWaitingForShipping".Translate(_testLanguageOverride), loadState.itemName, loadState.count, etaStr);
                    }

                    if (loadState.cost == null)
                    {
                        Log.Warn($"missed setting cost for waiting for shipping");
                        var etaStr = TimeUtil.FormatEta(loadState.secondsRemaining);
                        return string.Format("PLOGTaskWaitingForShipping".Translate(_testLanguageOverride), loadState.itemName, loadState.count, etaStr);
                    }

                    var stationInfo = StationInfo.ByPlanetIdStationId(loadState.cost.planetId, loadState.cost.stationId);
                    var planetName = stationInfo?.PlanetName ?? "Unknown";
                    var stationType = stationInfo?.StationType.ToString() ?? "Unknown";
                    if (planetName == "Unknown" || stationType == "Unknown")
                    {
                        Log.Warn($"Failed to get station info from cost: {loadState.cost.planetId}, {loadState.cost.stationId} {stationInfo}");
                    }

                    if (loadState.cost.needWarper)
                    {
                        return string.Format("PLOGShippingDelayedWarper".Translate(_testLanguageOverride), loadState.itemName, planetName, stationType);
                    }

                    return string.Format("PLOGShippingDelayedEnergy".Translate(_testLanguageOverride), loadState.itemName, planetName, stationType);
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