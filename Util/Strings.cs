using CommonAPI.Systems;

namespace PersonalLogistics.Util
{
    public static class Strings
    {
        public static void Init()
        {
            ProtoRegistry.RegisterString("PLOGplrequests", "Requests", "请求");
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
        }
    }
}