using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PersonalLogistics.Logistics;
using PersonalLogistics.Model;
using PersonalLogistics.ModPlayer;
using PersonalLogistics.SerDe;
using PersonalLogistics.Util;
using UnityEngine;
using static PersonalLogistics.Util.Log;

namespace PersonalLogistics.Shipping
{
    public class ShippingManager : InstanceSerializer
    {
        private readonly Dictionary<Guid, Cost> _costs = new();
        private ItemBuffer _itemBuffer;
        private readonly TimeSpan _minAge = TimeSpan.FromSeconds(60);
        private readonly Dictionary<Guid, ItemRequest> _requestByGuid = new();
        private readonly Queue<ItemRequest> _requests = new();
        private bool _loadedFromImport;
        private readonly PlogPlayerId _playerId;

        public ShippingManager(PlogPlayerId plogPlayerId)
        {
            _itemBuffer = new ItemBuffer();
            _playerId = plogPlayerId;
        }

        public override void ExportData(BinaryWriter w)
        {
            _itemBuffer.Export(w);
            Debug($"wrote {_itemBuffer.Count} buffered items");
            var itemRequests = new List<ItemRequest>(_requests)
                .FindAll(ir => ir.State != RequestState.Complete && ir.State != RequestState.Failed);
            w.Write(itemRequests.Count);
            foreach (var t in itemRequests)
            {
                if (t.FromRecycleArea)
                    continue;
                t.Export(w);
            }

            var costTuples = new List<(Guid guid, Cost cost)>();
            foreach (var cost in _costs)
            {
                var itemRequest = itemRequests.Find(ir => ir.guid == cost.Key);
                if (itemRequest == null)
                {
                    continue;
                }

                costTuples.Add((cost.Key, cost.Value));
            }

            w.Write(costTuples.Count);
            foreach (var costTuple in costTuples)
            {
                w.Write(costTuple.guid.ToString());
                costTuple.cost.Export(w);
            }

            Debug($"Wrote {itemRequests.Count} requests and {costTuples.Count} costs for shipping manager");
        }

        public override void ImportData(BinaryReader reader)
        {
            Import(reader);
        }


        private void Import(BinaryReader r)
        {
            try
            {
                Debug($"reading shipping manager data");

                _itemBuffer = ItemBuffer.Import(r);
                _loadedFromImport = true;
                var requestCount = r.ReadInt32();
                var requestsFromPlm = GetPlayer().personalLogisticManager.GetRequests() ?? new List<ItemRequest>();
                for (var i = requestCount - 1; i >= 0; i--)
                {
                    var itemRequest = ItemRequest.Import(r);
                    var requestFromPlm = requestsFromPlm.Find(req => req.guid == itemRequest.guid);
                    if (requestFromPlm != null)
                    {
                        itemRequest = requestFromPlm;
                        Debug($"replaced shipping mgr item request with instance from PLM {itemRequest.guid}");
                    }
                    else
                    {
                        Warn($"failed to replace shipping manager item request with actual from PLM. {itemRequest}");
                    }

                    _requests.Enqueue(itemRequest);
                    _requestByGuid[itemRequest.guid] = itemRequest;
                }

                var costCount = r.ReadInt32();
                for (var i = 0; i < costCount; i++)
                {
                    var costGuid = Guid.Parse(r.ReadString());
                    var cost = Cost.Import(r);
                    _costs[costGuid] = cost;
                }

                Debug($"Shipping manager read in {requestCount} requests and {costCount} costs");
            }
            catch (Exception e)
            {
                Warn("failed to Import shipping state");
            }
        }

        public bool AddToBuffer(int itemId, ItemStack stack)
        {
            if (!_itemBuffer.Add(itemId, stack))
            {
                Warn($"No more storage available for item {ItemUtil.GetItemName(itemId)}");
                return false;
            }

            Debug($"buffer updated with {stack.ItemCount} {stack.ProliferatorPoints}. ");
            return true;
        }

        public static void Process()
        {
            PlogPlayerRegistry.LocalPlayer().shippingManager.ProcessImpl();
        }

        private void ProcessImpl()
        {
            if (_requests.Count == 0)
            {
                SendBufferedItemsToNetwork();
                return;
            }

            var startTicks = DateTime.Now.Ticks;
            var timeSpan = new TimeSpan(DateTime.Now.Ticks - startTicks);
            ItemRequest firstRequest = null;
            while (_requests.Count > 0 && timeSpan < TimeSpan.FromMilliseconds(250))
            {
                timeSpan = new TimeSpan(DateTime.Now.Ticks - startTicks);
                var itemRequest = _requests.Dequeue();

                if (itemRequest.State == RequestState.Complete || itemRequest.State == RequestState.Failed)
                {
                    continue;
                }

                _requests.Enqueue(itemRequest);
                if (firstRequest == null)
                {
                    firstRequest = itemRequest;
                }
                else if (firstRequest.guid == itemRequest.guid)
                {
                    // we're done
                    break;
                }


                if (_costs.TryGetValue(itemRequest.guid, out var cost))
                {
                    if (cost.paid)
                    {
                        continue;
                    }

                    var supplyStationInfo = LogisticsNetwork.stations.FirstOrDefault(st =>
                        st.StationId == cost.stationId && st.PlanetInfo.PlanetId == cost.planetId);

                    if (cost.needWarper)
                    {
                        if (supplyStationInfo != null)
                        {
                            if (StationStorageManager.RemoveWarperFromStation(supplyStationInfo))
                            {
                                cost.needWarper = false;
                            }
                        }

                        if (cost.needWarper && !PluginConfig.neverUseMechaWarper.Value)
                        {
                            // get from player 
                            var cntToRemove = 1;
                            if (GetPlayer().inventoryManager
                                .RemoveItemImmediately(Mecha.WARPER_ITEMID, cntToRemove, out _))
                            {
                                cost.needWarper = false;
                                LogPopupWithFrequency("Personal logistics removed warper from player inventory");
                            }
                        }

                        if (cost.needWarper && cost.processingPassesCompleted > 20)
                        {
                            GetPlayer().personalLogisticManager.CancelInboundRequests(itemRequest.guid.ToString());
                        }
                    }

                    cost.energyCost = 0;
                    cost.needWarper = false;

                    if (cost.energyCost <= 0 && !cost.needWarper)
                    {
                        cost.paid = true;
                        cost.paidTick = GameMain.gameTick;
                    }
                    else
                    {
                        // since we are waiting on shipping but the cost isn't paid yet, need to advance completion time
                        // var computedTransitTime = itemRequest.ComputedCompletionTick - itemRequest.CreatedTick;
                        var stationInfo = StationInfo.ByPlanetIdStationId(cost.planetId, cost.stationId);
                        if (stationInfo == null)
                        {
                            Warn($"Shipping manager did not find station by planet: {cost.planetId} {cost.stationId}");
                        }
                        else
                        {

                            cost.needWarper = false;

                            itemRequest.ComputedCompletionTime = DateTime.Now;
                            itemRequest.ComputedCompletionTick = GameMain.gameTick;
                            Debug(
                                $"Advancing item request completion time to {itemRequest.ComputedCompletionTime} due to unpaid cost ({itemRequest.ComputedCompletionTick})");
                        }
                    }

                    cost.processingPassesCompleted++;
                }
            }

            var totalMilliseconds = new TimeSpan(DateTime.Now.Ticks - startTicks).TotalMilliseconds;
            if (totalMilliseconds < 250)
            {
                SendBufferedItemsToNetwork();
            }

            if (totalMilliseconds > 50)
                Debug($"Shipping completed after {totalMilliseconds} ms");
        }

        private void SendBufferedItemsToNetwork()
        {
            var pos = GetPlayer().GetPosition();
            foreach (var inventoryItem in _itemBuffer.GetInventoryItemView())
            {
                if (!IsOldEnough(inventoryItem))
                {
                    continue;
                }

                var desiredAmount = GetPlayer().inventoryManager.GetDesiredAmount(inventoryItem.itemId);
                if (desiredAmount.minDesiredAmount == 0 || !desiredAmount.allowBuffer ||
                    desiredAmount.maxDesiredAmount == 0)
                {
                    var remainingAmount = LogisticsNetwork.AddItem(pos.clusterPosition, inventoryItem.itemId,
                        inventoryItem.ToItemStack());

                    _itemBuffer.UpsertItem(inventoryItem.itemId, remainingAmount);
                }
                else if (inventoryItem.count > GameMain.history.logisticShipCarries)
                {
                    ItemStack totalBufferAmount = inventoryItem.ToItemStack();
                    var amountToRemove =
                        totalBufferAmount.Remove(inventoryItem.count - GameMain.history.logisticShipCarries);
                    var remaining = LogisticsNetwork.AddItem(pos.clusterPosition, inventoryItem.itemId, amountToRemove);
                    _itemBuffer.UpsertItem(inventoryItem.itemId, remaining);
                }
            }
        }


        private bool IsOldEnough(InventoryItem inventoryItem) =>
            TimeSpan.FromSeconds(inventoryItem.AgeInSeconds) > _minAge;

        public ItemStack RemoveFromBuffer(int itemId, int count)
        {
            if (!_itemBuffer.HasItem(itemId))
            {
                return ItemStack.Empty();
            }

            var removedStack = _itemBuffer.RemoveItemCount(itemId, count);

            removedStack = PluginConfig.BoostStackProliferator(removedStack);
            return removedStack;
        }

        public bool AddRequest(VectorLF3 playerUPosition, Vector3 playerLocalPosition, ItemRequest itemRequest)
        {
            var (_, removed, stationInfo) =
                LogisticsNetwork.RemoveItem(playerUPosition, playerLocalPosition, itemRequest.ItemId, itemRequest.ItemCount);
            if (removed.ItemCount == 0)
            {
                return false;
            }

            itemRequest.ComputedCompletionTime = DateTime.Now;
            var totalSeconds = (itemRequest.ComputedCompletionTime - DateTime.Now).TotalSeconds;
            itemRequest.ComputedCompletionTick = GameMain.gameTick +
                                                 TimeUtil.GetGameTicksFromSeconds(
                                                     Mathf.CeilToInt((float) totalSeconds));
            
            var addToBuffer = AddToBuffer(itemRequest.ItemId, removed);
            var actualBufferedItemCount = GetActualBufferedItemCount(itemRequest.ItemId);
            Debug(
                $"Added {removed.ItemCount}, {removed.ProliferatorPoints} of item to buffer {actualBufferedItemCount}");
            if (!addToBuffer)
            {
                Warn($"Failed to add inbound items to storage buffer {itemRequest.ItemId} {itemRequest.State}");
                LogisticsNetwork.AddItem(playerUPosition, itemRequest.ItemId, removed);
            }
            
            // update task to reflect amount that we actually have
            itemRequest.ItemCount = Math.Min(removed.ItemCount, itemRequest.ItemCount);
            _requests.Enqueue(itemRequest);
            _requestByGuid[itemRequest.guid] = itemRequest;
            _costs.Add(itemRequest.guid, CalculateCost(stationInfo, removed.ItemCount));
            return true;
        }

        private static Cost CalculateCost(StationInfo stationInfo, int actualShippingAmount)
        {
            return new Cost
            {
                energyCost = 0,
                needWarper = false,
                planetId = stationInfo.PlanetInfo.PlanetId,
                stationId = stationInfo.StationId,
                shippingToBufferCount = actualShippingAmount
            };
        }

        public bool ItemForTaskArrived(Guid requestGuid)
        {
            if (_requestByGuid.TryGetValue(requestGuid, out var request))
            {
                if (_costs.TryGetValue(requestGuid, out var cost))
                {
                    if (!cost.paid)
                    {
                        return false;
                    }
                }
                else
                {
                    Debug($"Cost not found for request {requestGuid} {request}, must be paid");
                }

                // check if arrival time is past
                if (GameMain.gameTick > request.ComputedCompletionTick)
                {
                    return true;
                }
            }

            return false;
        }

        public Cost GetCostForRequest(Guid requestGuid)
        {
            if (_costs.TryGetValue(requestGuid, out var cost))
            {
                return cost;
            }

            return null;
        }

        public int GetActualBufferedItemCount(int itemId) => _itemBuffer.GetItemCount(itemId);

        public override PlogPlayerId GetPlayerId() => _playerId;
        public override string GetExportSectionId() => "SM";

        public override void InitOnLoad()
        {
            _costs.Clear();
            _itemBuffer = new ItemBuffer();
            _requestByGuid.Clear();
            _requests.Clear();
            _loadedFromImport = true;
        }

        public override string SummarizeState() =>
            $"SM: {_itemBuffer.Count} bufferedItems, {_playerId}, {_costs.Count} costs, {_requests.Count} requests";

        public void UpsertBufferedItem(int itemId, int newItemCount, long gameTickUpdated, int packetProliferatorPoints)
        {
            _itemBuffer.UpsertItem(itemId, ItemStack.FromCountAndPoints(newItemCount, packetProliferatorPoints),
                gameTickUpdated);
        }
    }
}