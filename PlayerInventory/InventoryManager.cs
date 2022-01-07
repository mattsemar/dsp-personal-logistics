using System.Collections.Generic;
using System.IO;
using PersonalLogistics.Logistics;
using PersonalLogistics.Model;
using PersonalLogistics.ModPlayer;
using PersonalLogistics.Nebula;
using PersonalLogistics.Nebula.Client;
using PersonalLogistics.Scripts;
using PersonalLogistics.SerDe;
using PersonalLogistics.Util;
using UnityEngine;
using static PersonalLogistics.Util.Constant;

namespace PersonalLogistics.PlayerInventory
{
    public class InventoryManager : InstanceSerializer
    {
        public DesiredInventoryState desiredInventoryState = new(GameUtil.GetSeed());
        private PlogPlayerId _playerId;

        public InventoryManager(PlogPlayer player)
        {
            _playerId = player.playerId;
        }


        public DesiredInventoryState SaveInventoryAsDesiredState()
        {
            var inv = GameMain.mainPlayer.package;
            var itemCounts = new Dictionary<int, int>();
            for (var index = 0; index < inv.size; ++index)
            {
                var itemId = inv.grids[index].itemId;
                if (itemId == 0)
                {
                    continue;
                }

                var count = inv.grids[index].count;
                if (itemCounts.TryGetValue(itemId, out _))
                {
                    itemCounts[itemId] += count;
                }
                else
                {
                    itemCounts[itemId] = count;
                }
            }

            desiredInventoryState.ClearAll();
            foreach (var itemAndCount in itemCounts)
            {
                if (itemAndCount.Key == DEBUG_ITEM_ID)
                {
                    Log.Debug($"Adding item {itemAndCount.Key} {ItemUtil.GetItemName(itemAndCount.Key)} count={itemAndCount.Value} to desired list");
                }
                SetDesiredAmount(itemAndCount.Key, itemAndCount.Value, itemAndCount.Value);
            }

            foreach (var item in ItemUtil.GetAllItems())
            {
                if (!desiredInventoryState.IsDesiredOrBanned(item.ID))
                {
                    Log.Debug($"Adding item {item.ID} {ItemUtil.GetItemName(item.ID)} to ban list");
                    desiredInventoryState.AddBan(item.ID);
                }
            }

            return desiredInventoryState;
        }

        public static int GetMinRequestAmount(int itemId)
        {
            var inventoryManager = PlogPlayerRegistry.LocalPlayer().inventoryManager;
            return inventoryManager.GetDesiredAmount(itemId).minDesiredAmount;
        }

        public (int minDesiredAmount, int maxDesiredAmount, bool allowBuffer) GetDesiredAmount(int itemId)
        {
            if (!desiredInventoryState.IsDesiredOrBanned(itemId))
            {
                return (0, int.MaxValue, true);
            }

            if (desiredInventoryState.BannedItems.Contains(itemId))
            {
                return (0, 0, true);
            }

            if (desiredInventoryState.DesiredItems.TryGetValue(itemId, out var desiredItem))
            {
                return (desiredItem.count, desiredItem.maxCount, desiredItem.allowBuffering);
            }

            Log.Warn($"Unexpected state for item {itemId}. Not in ban list or desired list");
            return (-1, -1, true);
        }

        public DesiredItem GetDesiredItem(int itemId)
        {
            if (!desiredInventoryState.IsDesiredOrBanned(itemId))
            {
                return DesiredItem.notRequestedDesiredItem;
            }

            if (desiredInventoryState.BannedItems.Contains(itemId))
            {
                return DesiredItem.bannedDesiredItem;
            }

            if (desiredInventoryState.DesiredItems.TryGetValue(itemId, out var desiredItem))
            {
                return desiredItem;
            }

            Log.Warn($"Unexpected state for item {itemId}. Not in ban list or desired list");
            return DesiredItem.notRequestedDesiredItem;
        }

        public List<ItemRequest> GetFillBufferRequests()
        {
            var desiredItems = desiredInventoryState.GetAllDesiredItems();
            var result = new List<ItemRequest>();
            var shippingManager = PlogPlayerRegistry.LocalPlayer().shippingManager;
            if (shippingManager == null)
            {
                Log.Warn("shipping manager instance is null, that's very odd");
                return new List<ItemRequest>();
            }

            foreach (var desiredItem in desiredItems)
            {
                if (shippingManager.GetActualBufferedItemCount(desiredItem.ID) >= GameMain.history.logisticShipCarries)
                {
                    continue;
                }

                result.Add(new ItemRequest
                {
                    ItemCount = 1, ItemId = desiredItem.ID, RequestType = RequestType.Load, ItemName = desiredItem.Name.Translate(), SkipBuffer = false, fillBufferRequest = true
                });
            }

            return result;
        }

        public List<ItemRequest> GetItemRequests()
        {
            var inv = GameMain.mainPlayer.package;
            if (inv?.grids == null)
            {
                Log.Warn($"player package is null == {inv == null} || grids is null {inv?.grids == null}");
                return new List<ItemRequest>();
            }

            var itemCounts = new Dictionary<int, int>();
            for (var index = 0; index < inv.size; ++index)
            {
                var itemId = inv.grids[index].itemId;
                if (itemId < 1)
                {
                    continue;
                }

                var count = inv.grids[index].count;
                if (itemCounts.TryGetValue(itemId, out _))
                {
                    itemCounts[itemId] += count;
                }
                else
                {
                    itemCounts[itemId] = count;
                }
            }

            if (GameMain.mainPlayer.inhandItemId > 0 && GameMain.mainPlayer.inhandItemCount > 0)
            {
                itemCounts.TryGetValue(GameMain.mainPlayer.inhandItemId, out var value);
                itemCounts[GameMain.mainPlayer.inhandItemId] = value + GameMain.mainPlayer.inhandItemCount;
            }

            var result = new List<ItemRequest>(itemCounts.Keys.Count);
            foreach (var item in ItemUtil.GetAllItems())
            {
                var curCount = itemCounts.ContainsKey(item.ID) ? itemCounts[item.ID] : 0;
                var (action, actionCount, skipBuffer) =
                    desiredInventoryState.GetActionForItem(item.ID, curCount);
                if (DEBUG_ITEM_ID == item.ID)
                {
                    Log.Debug($"action for item {item.ID} {action} {actionCount}");
                }

                switch (action)
                {
                    case DesiredInventoryAction.None:
                        continue;
                    case DesiredInventoryAction.Add:
                        result.Add(new ItemRequest
                            { ItemCount = actionCount, ItemId = item.ID, RequestType = RequestType.Load, ItemName = item.Name.Translate(), SkipBuffer = skipBuffer });
                        break;
                    case DesiredInventoryAction.Remove:
                        result.Add(new ItemRequest
                            { ItemCount = actionCount, ItemId = item.ID, RequestType = RequestType.Store, ItemName = item.Name.Translate() });
                        break;
                }

                if (item.ID == DEBUG_ITEM_ID)
                {
                    Log.Debug($"Added new ItemRequest {result[result.Count - 1]}");
                }
            }

            return result;
        }

        public bool IsBanned(int itemId) => desiredInventoryState.BannedItems.Contains(itemId);


        public void ProcessInventoryActions()
        {
            if (Time.frameCount % 11 == 0)
            {
                ProcessInventoryAddRemoves();
            }

            if (Time.frameCount % 850 == 0 && PluginConfig.addFuelToMecha.Value)
            {
                AddFuelToMecha();
            }

            if (Time.frameCount % 411 == 0 && PluginConfig.addWarpersToMecha.Value)
            {
                AddWarpersToMecha();
            }
        }

        private void ProcessInventoryAddRemoves()
        {
            var logisticManager = GetPlayer().personalLogisticManager;
            if (logisticManager == null)
            {
                return;
            }

            var playerInventoryActions = logisticManager.GetInventoryActions();
            foreach (var action in playerInventoryActions)
            {
                if (action.ItemId == DEBUG_ITEM_ID)
                {
                    Log.Debug($"Performing inventory action {action}");
                }

                if (action.ActionType == PlayerInventoryActionType.Add)
                {
                    var removedFromBuffer = action.Request.bufferDebited ? action.ItemCount : 0;
                    if (!action.Request.bufferDebited)
                    {
                        removedFromBuffer += GetPlayer().shippingManager.RemoveFromBuffer(action.ItemId, action.ItemCount);
                    }

                    Log.Debug($"item request status is complete, remove from buffer {action.Request.ItemName}  {action.ItemCount}, actually removed {removedFromBuffer}");
                    var addItem = GameMain.mainPlayer.package.AddItem(action.ItemId, removedFromBuffer);
                    if (action.ItemId == DEBUG_ITEM_ID)
                    {
                        Log.Debug($"successful={addItem} added {ItemUtil.GetItemName(action.ItemId)} count={action.ItemCount}");
                    }

                    if (addItem < removedFromBuffer)
                    {
                        // inventory would not hold amount that we took out of buffer, add some back
                        var returnToBuffer = removedFromBuffer - addItem;
                        Log.Debug($"Re-adding {returnToBuffer} of {action.Request.ItemName} back into buffer");
                        GetPlayer().shippingManager.AddToBuffer(action.ItemId, returnToBuffer);
                    }

                    action.Request.State = RequestState.Complete;
                    if (addItem > 0)
                    {
                        UIItemup.Up(action.ItemId, addItem);
                    }
                }
                else if (action.ActionType == PlayerInventoryActionType.Remove)
                {
                    var itmId = action.ItemId;
                    var itmCnt = action.ItemCount;
                    if (action.Request.FromRecycleArea)
                    {
                        RecycleWindow.RemoveFromStorage(GridItem.From(action.Request.RecycleAreaIndex, itmId, itmCnt));
                    }
                    else
                    {
                        GameMain.mainPlayer.package.TakeTailItems(ref itmId, ref itmCnt);
                    }

                    var success = itmCnt == action.ItemCount;
                    action.Request.State = RequestState.Complete;
                    if (itmId == DEBUG_ITEM_ID)
                    {
                        Log.Debug($"successful={success} added {ItemUtil.GetItemName(action.ItemId)} count={itmCnt} (requestedCnt={action.ItemCount})");
                    }
                }
                else
                {
                    Log.Warn($"Unhandled action type {action} {action.ActionType}");
                }

                if (PluginConfig.sortInventory.Value)
                {
                    GameMain.mainPlayer.package.Sort();
                }
            }
        }

        private void AddFuelToMecha()
        {
            var storage = GameMain.mainPlayer?.mecha?.reactorStorage;
            if (storage == null)
            {
                return;
            }

            if (!storage.isFull)
            {
                var currentFuelIds = GetMechaFuelStorageItems(storage);
                var fuelItems = ItemUtil.GetFuelItemProtos();
                fuelItems.Sort((i1, i2) =>
                {
                    if (i1.ID == i2.ID) // should not actually happen
                    {
                        return 0;
                    }

                    var priority1 = GameMain.mainPlayer.mecha.reactorItemId == i1.ID ? -1000 : 0;
                    var priority2 = GameMain.mainPlayer.mecha.reactorItemId == i2.ID ? -1000 : 0;

                    if (currentFuelIds.Contains(i1.ID))
                    {
                        priority1 -= -500;
                    }

                    if (currentFuelIds.Contains(i2.ID))
                    {
                        priority2 -= -500;
                    }

                    if (GetMinRequestAmount(i1.ID) > 0)
                    {
                        priority1 -= 100;
                    }

                    if (GetMinRequestAmount(i2.ID) > 0)
                    {
                        priority2 -= 100;
                    }

                    if (priority1 != priority2)
                    {
                        return priority1.CompareTo(priority2);
                    }

                    return i2.HeatValue.CompareTo(i1.HeatValue);
                });
                foreach (var fuelItemProto in fuelItems)
                {
                    var itemCount = GameMain.mainPlayer?.package?.GetItemCount(fuelItemProto.ID) ?? 0;

                    if (itemCount > 0)
                    {
                        var addItemStacked = storage.AddItemStacked(fuelItemProto.ID, itemCount);
                        if (addItemStacked > 0)
                        {
                            RemoveItemImmediately(fuelItemProto.ID, addItemStacked);
                        }
                    }

                    if (storage.isFull)
                    {
                        break;
                    }
                }
            }
        }

        private void AddWarpersToMecha()
        {
            var mecha = GameMain.mainPlayer?.mecha;
            if (mecha == null)
            {
                return;
            }

            var storage = GameMain.mainPlayer?.mecha?.warpStorage;
            if (storage == null)
            {
                return;
            }

            if (mecha.warpStorage.isFull)
            {
                return;
            }

            if (!GameMain.history.ItemUnlocked(Mecha.WARPER_ITEMID))
            {
                return;
            }

            if (!LogisticsNetwork.HasItem(Mecha.WARPER_ITEMID))
            {
                return;
            }

            if (GetMinRequestAmount(Mecha.WARPER_ITEMID) > 0)
            {
                while (!storage.isFull)
                {
                    if (!RemoveItemImmediately(Mecha.WARPER_ITEMID, 1))
                    {
                        return;
                    }

                    storage.AddItem(Mecha.WARPER_ITEMID, 1);
                }
            }
        }

        // for testing mecha fuel top off
        private void AddFuelsToInventory()
        {
            var fuelItemProtos = ItemUtil.GetFuelItemProtos();
            var mainPlayerMecha = GameMain.mainPlayer?.mecha;
            if (mainPlayerMecha == null || GameMain.mainPlayer?.package == null)
            {
                return;
            }

            foreach (var itemProto in fuelItemProtos)
            {
                var amountToAdd = itemProto.StackSize * 4;

                if (GameMain.mainPlayer?.package.GetItemCount(itemProto.ID) >= amountToAdd)
                {
                    continue;
                }

                amountToAdd -= GameMain.mainPlayer.package.GetItemCount(itemProto.ID);
                AddItemToInventory(itemProto.ID, amountToAdd);
            }
        }

        private HashSet<int> GetMechaFuelStorageItems(StorageComponent storageComponent)
        {
            var result = new HashSet<int>();
            foreach (var grid in storageComponent.grids)
            {
                if (grid.itemId > 0)
                {
                    result.Add(grid.itemId);
                }
            }

            return result;
        }

        public void BanItem(int itemID)
        {
            desiredInventoryState.AddBan(itemID);
        }

        public void UnBanItem(int itemID)
        {
            desiredInventoryState.BannedItems.Remove(itemID);
        }

        public void SetDesiredAmount(int itemID, int newValue, int maxValue)
        {
            if (maxValue == 0)
            {
                BanItem(itemID);
                if (NebulaLoadState.IsMultiplayerClient())
                    RequestClient.SendDesiredItemUpdate(itemID, 0, 0);
                return;
            }

            if (desiredInventoryState.BannedItems.Contains(itemID))
            {
                desiredInventoryState.BannedItems.Remove(itemID);
            }

            desiredInventoryState.AddDesiredItem(itemID, newValue, maxValue);
            if (NebulaLoadState.IsMultiplayerClient())
            {
                RequestClient.SendDesiredItemUpdate(itemID, newValue, maxValue);
            }
        }

        public void Clear()
        {
            desiredInventoryState.ClearAll();
            // todo send a notification for this
        }

        public bool RemoveItemImmediately(int itemId, int count)
        {
            // TODO support remote
            var cnt = count;
            GameMain.mainPlayer.package.TakeTailItems(ref itemId, ref cnt);
            if (PluginConfig.sortInventory.Value)
            {
                GameMain.mainPlayer.package.Sort();
            }

            return cnt == count;
        }

        public int AddItemToInventory(int itemId, int itemCount)
        {
            var added = GameMain.mainPlayer.package.AddItem(itemId, itemCount);

            if (added > 0)
            {
                UIItemup.Up(itemId, added);
            }

            if (added > 0 && PluginConfig.sortInventory.Value)
            {
                GameMain.mainPlayer.package.Sort();
            }

            return added;
        }

        public void ToggleBuffering(int itemID)
        {
            if (desiredInventoryState.DesiredItems.ContainsKey(itemID))
            {
                desiredInventoryState.DesiredItems[itemID].allowBuffering = !desiredInventoryState.DesiredItems[itemID].allowBuffering;
            }
            else
            {
                Log.Warn($"Item {itemID} not found. Buffering will not be toggled");
            }
        }

        public override void ExportData(BinaryWriter w)
        {
            desiredInventoryState.Export(w);
        }

        public override void ImportData(BinaryReader reader)
        {
            Log.Debug($"importing desiredInvState");
            desiredInventoryState = DesiredInventoryState.Import(reader);
        }

        public override PlogPlayerId GetPlayerId() => _playerId;

        public override string GetExportSectionId() => "DINV";

        public override void InitOnLoad()
        {
            desiredInventoryState.ClearAll();            
        }
        public override string SummarizeState() => $"DINV: {desiredInventoryState.BannedItems.Count} banned, {desiredInventoryState.DesiredItems.Count} desired count";
    }
}