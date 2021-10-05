using System;
using System.Collections.Generic;
using System.Linq;

namespace PersonalLogistics.Util
{
    public class ItemUtil
    {
        public static List<ItemProto> GetAllItems()
        {
            return LDB._items.dataArray.ToList().FindAll(i => GameMain.history.ItemUnlocked(i.ID));
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
    }
}