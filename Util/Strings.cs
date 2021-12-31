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
                "CTRL click for max\r\nShift click for 5");
            ProtoRegistry.RegisterString("PLOGmultipletiptitle", "Multiple");
            ProtoRegistry.RegisterString("PLOGEnableRequestRecycle", "Enable and auto-recycle");

            ProtoRegistry.RegisterString("PLOGOk", "OK", "行");
            ProtoRegistry.RegisterString("PLOGCancel", "Cancel", "否");
            ProtoRegistry.RegisterString("PLOGPause", "Pause", "停顿");
            ProtoRegistry.RegisterString("PLOGPlay", "Play", "恢复");
            ProtoRegistry.RegisterString("(Banned) recycle this item immediately if found in inventory", "(Banned) recycle this item immediately if found in inventory", "禁止，自动回收");
            ProtoRegistry.RegisterString("PLOGTrash management title", "Personal Logistics Trash Management", "个人物流垃圾管理");
            ProtoRegistry.RegisterString("PLOGTrash management popup message",
                "The Personal Logistics mod is configured to send trashed items to logistics stations. \r\n" +
                "This dialog is to let you know that the feature is currently enabled.\r\n" +
                "If this behavior is not what you want then click \"Cancel\" and the feature will be disabled\r\n" +
                "If you click \"Ok\" then this popup will not be shown again.",
                "确认发送垃圾物品至物流网络");

            ProtoRegistry.RegisterString("KEYShowPlogWindow", "Show Personal Logistics Window");
            
            // Incoming items stuff
            // item name should be {0}
            ProtoRegistry.RegisterString("PLOGLoadingFromBuffer", "Loading {0} from buffer", "从缓冲区加载{0}", "Chargement de {0} à partir du tampon");
            ProtoRegistry.RegisterString("PLOGTaskCreated", "Task created to load {0}", "为项目{0}创建的任务");
            ProtoRegistry.RegisterString("PLOGTaskFailed", "Failed to load {0} from logistics stations", "无法从物流网络加载{0}", "Échec du chargement de {0} depuis le réseau logistique");
            // item name {0}, amount is {1} and formatted ETA string is {2}
            ProtoRegistry.RegisterString("PLOGTaskWaitingForShipping", "{0} (x{1}) ETA {2} in buffer", "{0} ({1}) {2}", "{0} (x{1}) {2}");
        }
    }
}