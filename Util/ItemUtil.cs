using System;
using System.Collections.Generic;
using System.Linq;

namespace PersonalLogistics.Util
{
    public class ItemUtil
    {
        private static List<ItemProto> _cachedFuelItemProtos = new List<ItemProto>();
        private static DateTime _fuelItemCacheTs = DateTime.Now.AddDays(-1);

        public static List<ItemProto> GetAllItems()
        {
            return LDB._items.dataArray.ToList().FindAll(i => i.ID > 0 && GameMain.history.ItemUnlocked(i.ID));
        }

        public static HashSet<EItemType> GetAllItemTypes()
        {
            return new HashSet<EItemType>(GetAllItems().Select(i => i.Type));
        }

        public static string GetItemName(int itemId)
        {
            try
            {
                return LDB._items.Select(itemId).Name.Translate();
            }
            catch (Exception e)
            {
                Log.Warn($"failed to get item name {itemId} {e.Message}\n{e.StackTrace}");
            }

            return $"__UNKNOWN_{itemId}__UNKNOWN__";
        }

        public static ItemProto GetItemProto(int itemId)
        {
            return LDB._items.Select(itemId);
        }

        public static List<ItemProto> GetFuelItemProtos()
        {
            if ((DateTime.Now - _fuelItemCacheTs).TotalSeconds > 100)
            {
                Log.Debug("refreshing fuel item cache list");
                _fuelItemCacheTs = DateTime.Now;
                _cachedFuelItemProtos = LDB._items.dataArray.ToList().FindAll(i => i.ID > 0 && GameMain.history.ItemUnlocked(i.ID) && i.FuelType > 0);
                return _cachedFuelItemProtos;
            }

            return _cachedFuelItemProtos;
        }
    }
}