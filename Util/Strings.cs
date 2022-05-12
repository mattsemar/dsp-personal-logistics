using BepInEx.Configuration;
using CommonAPI.Systems;
using HarmonyLib;
using xiaoye97;

namespace PersonalLogistics.Util
{
    public static class Strings
    {
        public static void Init()
        {
            RegisterString("PLOGplrequests", "Requests", "请求");
            RegisterString("PLOGrequest", "Request", "请求");
            RegisterString("PLOGrecycle", "Recycle", "回收");

            RegisterString("PLOGCurrent", "Current", "当前");
            RegisterString("PLOGUpdated", "Updated", "新的");

            RegisterString("Drop items here to recycle", "Drop items here to recycle", "将物品扔到这里进行回收");

            RegisterString("PLOGmaxallowed",
                "Maximum stacks allowed in inventory, setting this value to 0 means the item is banned from your inventory. Items in inventory above this amount will be sent to logistics stations",
                "最大允许数");
            RegisterString("PLOGminallowed",
                "Number of stacks requested of this item, if amount in inventory is less than this value then the item will be requested. Setting to zero means this item will not be fetched",
                "要求金额");
            RegisterString("PLOGsavechanges",
                "Save",
                "节省");
            RegisterString("PLOGmultipletip",
                "CTRL click for max\r\nShift click for 5");
            RegisterString("PLOGmultipletiptitle", "Multiple");
            RegisterString("PLOGEnableRequestRecycle", "Enable and auto-recycle");

            RegisterString("PLOGOk", "OK", "行");
            RegisterString("PLOGCancel", "Cancel", "否");
            RegisterString("PLOGPause", "Pause", "停顿");
            RegisterString("PLOGPlay", "Play", "恢复");
            RegisterString(
                "(Banned) recycle this item immediately if found in inventory",
                "(Banned) recycle this item immediately if found in inventory",
                "禁止，自动回收");
            RegisterString("PLOGTrash management title",
                "Personal Logistics Trash Management",
                "个人物流垃圾管理");
            RegisterString("PLOGTrash management popup message",
                "The Personal Logistics mod is configured to send trashed items to logistics stations. \r\n" +
                "This dialog is to let you know that the feature is currently enabled.\r\n" +
                "If this behavior is not what you want then click \"Cancel\" and the feature will be disabled\r\n" +
                "If you click \"Ok\" then this popup will not be shown again.",
                "确认发送垃圾物品至物流网络");

            RegisterString("KEYShowPlogWindow", "Show Personal Logistics Window");

            // Incoming items stuff
            // item name should be {0}, count is {1}
            RegisterString("PLOGLoadingFromBuffer",
                "Loading {0} ({1}) from buffer to inventory",
                "从缓冲区加载{0} ({1})",
                "Chargement de {0} à partir du tampon ({1})");
            RegisterString("PLOGTaskCreated",
                "Task created to load {0} ({1})",
                "为项目{0}创建的任务 ({1})");
            RegisterString("PLOGTaskFailed",
                "Failed to load {0} from logistics stations",
                "无法从物流网络加载{0}",
                "Échec du chargement de {0} depuis le réseau logistique");
            // item name {0}, amount is {1} and formatted ETA string is {2}
            RegisterString("PLOGTaskWaitingForShipping",
                "{0} ({1}) in-transit to local buffer, ETA {2}",
                "{0} ({1}) {2}",
                "{0} ({1}) {2}");
            RegisterString("PLOGShippingDelayedWarper",
                "Shipment of {0} from {1} is delayed, missing warpers in {2}",
                "发货延迟 {0} ({1}) {2}"
                );
            RegisterString("PLOGShippingDelayedEnergy",
                "Shipment of {0} from {1} is delayed, missing energy in {2}",
                "发货延迟 {0} ({1}) {2}"
                );
            RegisterString("PLOGShippingCostProcessing",
                "Cost calculation for {0} ({1}) pending",
                "{0} ({1}) 有待");
        }

        private static void RegisterString(string key, string enTrans, string cnTrans = "", string frTrans = "")
        {
            var configDefinition = new ConfigDefinition("String", key);
            {
                if (Traverse.Create(typeof(LDBTool)).Field("CustomStringENUS").GetValue() is ConfigFile enUsConfig)
                {
                    var configEntry = enUsConfig.Bind(configDefinition, enTrans, ConfigDescription.Empty);
                    if (configEntry.Value != enTrans)
                        Log.Debug($"Explicitly setting EN translation for {key}");
                    configEntry.Value = enTrans;
                }
            }
            {
                if (!string.IsNullOrEmpty(frTrans) && Traverse.Create(typeof(LDBTool)).Field("CustomStringFRFR").GetValue() is ConfigFile frFrConfig)
                {
                    var configEntry = frFrConfig.Bind(configDefinition, frTrans, ConfigDescription.Empty);
                    configEntry.Value = frTrans;
                }
            }
            {
                if (!string.IsNullOrEmpty(cnTrans) && Traverse.Create(typeof(LDBTool)).Field("CustomStringZHCN").GetValue() is ConfigFile zhCnConfig)
                {
                    var configEntry = zhCnConfig.Bind(configDefinition, cnTrans, ConfigDescription.Empty);
                    configEntry.Value = cnTrans;
                }
            }
            ProtoRegistry.RegisterString(key, enTrans, cnTrans, frTrans);
        }
    }
}