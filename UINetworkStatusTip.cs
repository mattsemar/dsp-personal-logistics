using System;
using System.Collections.Generic;
using System.Linq;
using PersonalLogistics.Logistics;
using PersonalLogistics.PlayerInventory;
using UnityEngine;
using static PersonalLogistics.Log;
using Object = UnityEngine.Object;

namespace PersonalLogistics
{
    public class UINetworkStatusTip
    {
        private readonly UIItemTip _instance;
        private static readonly Dictionary<UINetworkStatusTip, UIItemTip> _parentTips = new Dictionary<UINetworkStatusTip, UIItemTip>();
        private static readonly HashSet<UIItemTip> _tipsCreatedByUs = new HashSet<UIItemTip>();
        private long _tipUpdateTime;

        public static bool IsOurTip(UIItemTip tip)
        {
            return _tipsCreatedByUs.Contains(tip);
        }

        private readonly int BELOW_LEFT = 1;
        private readonly int BELOW_CENTER = 2;
        private readonly int BELOW_RIGHT = 3;
        private readonly int LEFT_CENTER_HEIGHT = 4;
        private readonly int CENTER_ON_TOP_OF = 5;
        private readonly int RIGHT_CENTER_HEIGHT = 6;
        private readonly int ABOVE_CENTER = 8;
        private readonly int BELOW_CORNER = 7;
        private readonly int ABOVE_RIGHT = 9;

        private UINetworkStatusTip(UIItemTip parentTip)
        {
            _instance = Object.Instantiate(Configs.builtin.uiItemTipPrefab, parentTip.trans, false);
            _tipsCreatedByUs.Add(_instance);
            var corner = BELOW_CENTER;
            var mouseInRightThird = Input.mousePosition.x > (Screen.width * 2.0f/3.0f);
            var mouseInBottomHalf = Input.mousePosition.y < Screen.height / 2;
            if (mouseInRightThird && mouseInBottomHalf)
            {
                corner = LEFT_CENTER_HEIGHT;
            } else if (mouseInBottomHalf)
            {
                corner = RIGHT_CENTER_HEIGHT;
            }
           
            _instance.SetTip(0, corner, new Vector2(0, 0), parentTip.transform, true);
            _instance.nameText.text = $"Personal logistics";
            var (minDesiredAmount, maxDesiredAmount) = InventoryManager.Instance.GetDesiredAmount(parentTip.showingItemId);
            _instance.categoryText.text = "";
            if (minDesiredAmount == 0 && maxDesiredAmount > 10000)
            {
                // neither banned nor requested
                _instance.categoryText.text = "Not managed by Personal Logistics";
            }
            else if (maxDesiredAmount == 0 && minDesiredAmount == 0)
            {
                _instance.categoryText.text = "Banned from player inventory";
            }
            else if (minDesiredAmount > 0)
            {
                _instance.categoryText.text = $"Request min {minDesiredAmount}, max {maxDesiredAmount}";
            }

            _instance.propsText.text = "";
            _instance.descText.text = LogisticsNetwork.ItemSummary(parentTip.showingItemId);
            _tipUpdateTime = DateTime.Now.Ticks;
            _instance.recipeEntry.gameObject.SetActive(false);
            _instance.sepLine.gameObject.SetActive(false);
            _instance.valuesText.text = "";
            Object.Destroy(_instance.iconImage.gameObject);
            _instance.descText.rectTransform.anchoredPosition = new Vector2(Configs.builtin.uiItemTipPrefab.descText.rectTransform.anchoredPosition.x, -47f);
            ClearOthers(this);
        }

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
                _parentTips.Remove(removedTip);
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
                if (!_parentTips[tip].gameObject.activeSelf)
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
                _parentTips.Remove(removedTip);
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
            long elapsedTicks = DateTime.Now.Ticks - _tipUpdateTime;
            TimeSpan elapsedSpan = new TimeSpan(elapsedTicks);
            if (elapsedSpan > TimeSpan.FromSeconds(5))
            {
                _tipUpdateTime = DateTime.Now.Ticks;
                _instance.descText.text = LogisticsNetwork.ItemSummary(_parentTips[this].showingItemId);
            }
        }

        public static void Create(UIItemTip uiItemTip)
        {
            if (IsOurTip(uiItemTip))
                return;
            var newInstance = new UINetworkStatusTip(uiItemTip);
            _tipsCreatedByUs.Add(newInstance._instance);
            _parentTips.Add(newInstance, uiItemTip);
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
                _parentTips.Remove(uiNetworkStatusTip);
                Object.Destroy(uiNetworkStatusTip._instance.gameObject);
            }
            catch (Exception e)
            {
                Warn($"exception closing tip windows {e.Message} \r\n {e.StackTrace}");
            }
        }
    }
}