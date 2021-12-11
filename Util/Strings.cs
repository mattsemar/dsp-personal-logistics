using CommonAPI.Systems;

namespace PersonalLogistics.Util
{
    public static class Strings
    {
        public static void Init()
        {
            ProtoRegistry.RegisterString("PLOGplrequests", "Requests", "请求");
            ProtoRegistry.RegisterString("PLOGrequest", "Request", "请求");
            ProtoRegistry.RegisterString("PLOGrecycle", "Recycle", "回收");

            ProtoRegistry.RegisterString("PLOGCurrent", "Current", "当前");
            ProtoRegistry.RegisterString("PLOGUpdated", "Updated", "新的");
            
            ProtoRegistry.RegisterString("Drop items here to recycle", "Drop items here to recycle", "将物品扔到这里进行回收");
            
            ProtoRegistry.RegisterString("PLOGmaxallowed",
                "Maximum stacks allowed in inventory, setting this value to 0 means the item is banned from your inventory. Items in inventory above this amount will be sent to logistics stations",
                "最大允许数");
            ProtoRegistry.RegisterString("PLOGminallowed",
                "Number of stacks requested of this item, if amount in inventory is less than this value then the item will be requested. Setting to zero means this item will not be fetched",
                "要求金额");
            ProtoRegistry.RegisterString("PLOGsavechanges",
                "Save",
                "节省");
            ProtoRegistry.RegisterString("PLOGmultipletip",
                "CTRL Click for max\r\nShift click for 5");
            ProtoRegistry.RegisterString("PLOGmultipletiptitle",
                "Multiple");

            
            ProtoRegistry.RegisterString("(Banned) recycle this item immediately if found in inventory", "(Banned) recycle this item immediately if found in inventory","禁止，自动回收");
        }
    }
}