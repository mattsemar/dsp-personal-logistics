using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PersonalLogistics.Util
{
    public static class ItemUtil
    {
        private static List<ItemProto> _cachedFuelItemProtos = new List<ItemProto>();
        private static DateTime _fuelItemCacheTs = DateTime.Now.AddDays(-1);

        public static List<ItemProto> GetAllItems()
        {
            return LDB.items.dataArray.ToList().FindAll(i => i.ID > 0 && GameMain.history.ItemUnlocked(i.ID));
        }

        public static HashSet<EItemType> GetAllItemTypes()
        {
            return new HashSet<EItemType>(GetAllItems().Select(i => i.Type));
        }

        public static string GetItemName(int itemId)
        {
            try
            {
                return LDB.items.Select(itemId).Name.Translate();
            }
            catch (Exception e)
            {
                Log.Warn($"failed to get item name {itemId} {e.Message}\n{e.StackTrace}");
            }

            return $"__UNKNOWN_{itemId}__UNKNOWN__";
        }

        public static ItemProto GetItemProto(int itemId) => LDB.items.Select(itemId);

        public static List<ItemProto> GetFuelItemProtos()
        {
            if ((DateTime.Now - _fuelItemCacheTs).TotalSeconds > 100)
            {
                Log.Debug("refreshing fuel item cache list");
                _fuelItemCacheTs = DateTime.Now;
                _cachedFuelItemProtos = LDB.items.dataArray.ToList().FindAll(i => i.ID > 0 && GameMain.history.ItemUnlocked(i.ID) && i.FuelType > 0);
                return _cachedFuelItemProtos;
            }

            return _cachedFuelItemProtos;
        }

        public static int CalculateStacksFromItemCount(int itemId, int itemCount, int stackSize= 0)
        {
            if (itemCount == 0)
                return 0;
            if (stackSize > 0)
            {
                return Mathf.CeilToInt(itemCount / (float) stackSize);
            }

            if (itemId == 0)
            {
                Log.Debug($"called with itemId {itemId}, {itemCount}, {stackSize}: {StackTraceUtility.ExtractStackTrace()}");
                return 1;
            }

            return Mathf.CeilToInt(itemCount / (float)GetStackSize(itemId));
        }

        public static int GetStackSize(int itemId)
        {
            var itemProto = GetItemProto(itemId);
            if (itemProto == null)
            {
                // item may be referred to in config but not in game anymore. 1 should be a reasonable number to put here
                Log.Debug($"item proto for item id: {itemId} not found. Return stackSize = 1");
                return 1;
            }

            return itemProto.StackSize;
        }
    }
}