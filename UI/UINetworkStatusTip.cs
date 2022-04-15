using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using PersonalLogistics.Logistics;
using UnityEngine;
using static PersonalLogistics.ModPlayer.PlogPlayerRegistry;
using static PersonalLogistics.Util.Log;
using Object = UnityEngine.Object;

namespace PersonalLogistics.UI
{
    public class UINetworkStatusTip
    {
        private static readonly ConcurrentDictionary<UINetworkStatusTip, UIItemTip> _parentTips = new();
        private static readonly HashSet<UIItemTip> _tipsCreatedByUs = new();
        private readonly UIItemTip _instance;
        private readonly int ABOVE_CENTER = 8;
        private readonly int ABOVE_RIGHT = 9;
        private readonly int BELOW_CENTER = 2;
        private readonly int BELOW_CORNER = 7;

        private readonly int BELOW_LEFT = 1;
        private readonly int BELOW_RIGHT = 3;
        private readonly int CENTER_ON_TOP_OF = 5;
        private readonly int LEFT_CENTER_HEIGHT = 4;
        private readonly int RIGHT_CENTER_HEIGHT = 6;
        private long _tipUpdateTime;

        private UINetworkStatusTip(UIItemTip parentTip)
        {
            _instance = Object.Instantiate(Configs.builtin.uiItemTipPrefab, parentTip.trans, false);
            _tipsCreatedByUs.Add(_instance);
            var corner = BELOW_CENTER;
            var mouseInRightThird = Input.mousePosition.x > Screen.width * 2.0f / 3.0f;
            var mouseInBottomTwoThirds = Input.mousePosition.y < (2.0f * Screen.height / 3.0f);
            if (mouseInRightThird && mouseInBottomTwoThirds)
            {
                corner = LEFT_CENTER_HEIGHT;
            }
            else if (mouseInBottomTwoThirds)
            {
                corner = RIGHT_CENTER_HEIGHT;
            }

            _instance.SetTip(0, corner, new Vector2(0, 0), parentTip.transform, 0,0, UIButton.ItemTipType.Item);
            _instance.nameText.text = "Personal logistics";
            var desiredItem = LocalPlayer().inventoryManager.GetDesiredItem(parentTip.showingItemId);
            _instance.categoryText.text = "";
            if (desiredItem.IsNonRequested() && !desiredItem.IsRecycle())
            {
                // neither banned nor requested
                _instance.categoryText.text = "Not managed by Personal Logistics";
            }
            else if (desiredItem.IsBanned())
            {
                _instance.categoryText.text = "Banned from player inventory";
            }
            else
            {
                _instance.categoryText.text = $"Request stacks {desiredItem.RequestedStacks()}";
                if (desiredItem.IsRecycle())
                {
                    _instance.categoryText.text += $", recycle over {desiredItem.RecycleMaxStacks()}";
                }
            }

            _instance.propsText.text = "";
            var summaryResult = LogisticsNetwork.ItemSummary(parentTip.showingItemId);
            _instance.descText.text = summaryResult.summaryTxt;
            if (!summaryResult.hitNull)
                _tipUpdateTime =  DateTime.Now.Ticks;
            _instance.recipeEntry.gameObject.SetActive(false);
            _instance.sepLine.gameObject.SetActive(false);
            _instance.valuesText.text = "";
            Object.Destroy(_instance.iconImage.gameObject);
            _instance.descText.rectTransform.anchoredPosition = new Vector2(Configs.builtin.uiItemTipPrefab.descText.rectTransform.anchoredPosition.x, -47f);
            ClearOthers(this);
        }

        public static bool IsOurTip(UIItemTip tip) => _tipsCreatedByUs.Contains(tip);

        private void ClearOthers(UINetworkStatusTip ourOnlyTip)
        {
            var removedTips = new List<UINetworkStatusTip>();

            foreach (var tip in _parentTips.Keys)
            {
                if (tip != ourOnlyTip)
                {
                    try
                    {
                        _tipsCreatedByUs.Remove(tip._instance);
                        removedTips.Add(tip);
                    }
                    catch (Exception e)
                    {
                        Warn($"exception while closing tip {e}\n{e.StackTrace}");
                    }
                }
            }

            foreach (var removedTip in removedTips)
            {
                _parentTips.TryRemove(removedTip, out _);
                try
                {
                    Object.Destroy(removedTip._instance.gameObject);
                }
                catch (Exception e)
                {
                    Warn($"Exception while destroying game object {e.Message}\n{e.StackTrace}");
                }
            }
        }

        public static void UpdateAll()
        {
            var removedTips = new List<UINetworkStatusTip>();
            foreach (var tip in _parentTips.Keys)
            {
                if (_parentTips[tip] == null || _parentTips[tip].gameObject == null || !_parentTips[tip].gameObject.activeSelf)
                {
                    try
                    {
                        _tipsCreatedByUs.Remove(tip._instance);
                        removedTips.Add(tip);
                    }
                    catch (Exception e)
                    {
                        Warn($"exception while closing tip {e}\n{e.StackTrace}");
                    }
                }
                else
                {
                    tip.RefreshText();
                }
            }

            foreach (var removedTip in removedTips)
            {
                _parentTips.TryRemove(removedTip, out _);
                try
                {
                    Object.Destroy(removedTip._instance.gameObject);
                }
                catch (Exception e)
                {
                    Warn($"Exception while destroying game object {e.Message}\n{e.StackTrace}");
                }
            }
        }

        private void RefreshText()
        {
            var elapsedTicks = DateTime.Now.Ticks - _tipUpdateTime;
            var elapsedSpan = new TimeSpan(elapsedTicks);
            if (elapsedSpan > TimeSpan.FromSeconds(5))
            {
                var itemSummaryResult = LogisticsNetwork.ItemSummary(_parentTips[this].showingItemId);
                _instance.descText.text = itemSummaryResult.summaryTxt;
                if (!itemSummaryResult.hitNull)
                    _tipUpdateTime = DateTime.Now.Ticks;
            }
        }

        public static void Create(UIItemTip uiItemTip)
        {
            if (IsOurTip(uiItemTip))
            {
                return;
            }

            var newInstance = new UINetworkStatusTip(uiItemTip);
            _tipsCreatedByUs.Add(newInstance._instance);
            _parentTips.TryAdd(newInstance, uiItemTip);
        }

        public static void CloseTipWindow(UIItemTip tipWindow)
        {
            try
            {
                var uiNetworkStatusTip = _parentTips.Keys.FirstOrDefault(pt => pt._instance == tipWindow);
                if (uiNetworkStatusTip == null)
                {
                    Object.Destroy(tipWindow.gameObject);
                    return;
                }

                _tipsCreatedByUs.Remove(uiNetworkStatusTip._instance);
                _parentTips.TryRemove(uiNetworkStatusTip, out _);
                Object.Destroy(uiNetworkStatusTip._instance.gameObject);
            }
            catch (Exception e)
            {
                Warn($"exception closing tip windows {e.Message} \r\n {e.StackTrace}");
            }
        }
    }
}