using System;
using System.Collections.Generic;
using PersonalLogistics.Shipping;
using PersonalLogistics.Util;
using static PersonalLogistics.Util.Log;

namespace PersonalLogistics.PlayerInventory
{
    public class TrashTask
    {
        public int itemId;
    }

    /// <summary>Manages sending trash to network </summary>
    public static class TrashHandler
    {
        public static TrashSystem trashSystem;
        public static Player player;
        private static readonly Queue<TrashTask> _tasks = new Queue<TrashTask>();
        private static readonly Dictionary<int, TrashTask> _taskLookupByItemId = new Dictionary<int, TrashTask>();
        private static bool _popupShowing;

        public static void AddTask(int itemId)
        {
            if (_taskLookupByItemId.ContainsKey(itemId))
            {
                return;
            }

            var trashTask = new TrashTask
            {
                itemId = itemId
            };
            _taskLookupByItemId.Add(itemId, trashTask);
            _tasks.Enqueue(trashTask);
        }

        public static void ProcessTasks()
        {
            if (_tasks.Count == 0 || _popupShowing)
            {
                return;
            }

            if (!PluginConfig.playerConfirmedTrash.Value && _tasks.Count > 0)
            {
                _popupShowing = true;
                UIMessageBox.Show("PLOGTrash management title".Translate(), "PLOGTrash management popup message".Translate(),
                    "PLOGOk".Translate(),
                    "PLOGCancel".Translate(),
                    1,
                    () =>
                    {
                        PluginConfig.playerConfirmedTrash.Value = true;
                        _popupShowing = false;
                    },
                    () =>
                    {
                        PluginConfig.sendLitterToLogisticsNetwork.Value = false;
                        PluginConfig.playerConfirmedTrash.Value = true;
                        _popupShowing = false;
                    }
                );
                return;
            }

            if (!PluginConfig.sendLitterToLogisticsNetwork.Value)
            {
                _tasks.Clear();
                return;
            }

            var startTicks = DateTime.Now.Ticks;
            var timeSpan = new TimeSpan(DateTime.Now.Ticks - startTicks);

            while (_tasks.Count > 0 && timeSpan < TimeSpan.FromMilliseconds(250))
            {
                var trashTask = _tasks.Dequeue();
                _taskLookupByItemId.Remove(trashTask.itemId);
                var removedCount = 0;
                var container = trashSystem.container;
                var trashObjPool = container.trashObjPool;
                var trashDataPool = container.trashDataPool;
                var trashCursor = container.trashCursor;
                var totalTrashOfObjectType = 0;
                for (var index = 0; index < trashCursor; ++index)
                {
                    var trashObject = trashObjPool[index];

                    if (trashObject.item == trashTask.itemId && trashObject.expire < 0)
                    {
                        var trashData = trashDataPool[index];
                        totalTrashOfObjectType += trashObject.count;
                        var distance = player.uPosition.Distance(trashData.uPos);
                        if (distance < 1000)
                        {
                            // found item to remove
                            removedCount += trashObject.count;
                            container.RemoveTrash(index);
                        }
                        else
                        {
                            var planetById = GameMain.galaxy.PlanetById(trashData.nearPlanetId);
                            var planetName = planetById == null ? "unknown planet" : planetById.displayName.Translate();
                            Debug($"Trashed item too far away for cleanup. Distance: {distance}. {planetName}");
                        }
                    }
                }

                if (removedCount > 0)
                {
                    // TODO check that trashed items were actually added to buffer successfully
                    if (ShippingManager.AddToBuffer(trashTask.itemId, removedCount))
                    {
                        var elapsed = new TimeSpan(DateTime.Now.Ticks - startTicks);
                        Debug(
                            $"Sent {removedCount}/{totalTrashOfObjectType} trashed items {ItemUtil.GetItemName(trashTask.itemId)} to local buffer (runtime: {elapsed.Milliseconds} ms)");
                    }
                }

                timeSpan = new TimeSpan(DateTime.Now.Ticks - startTicks);
            }

            var totalRuntime = new TimeSpan(DateTime.Now.Ticks - startTicks);

            Debug($"TrashHandler completed: {_tasks.Count} remaining tasks, elapsed time = {totalRuntime.Milliseconds} ms");
        }
    }
}