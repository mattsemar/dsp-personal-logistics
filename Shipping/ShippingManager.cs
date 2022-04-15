using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NebulaAPI;
using PersonalLogistics.Logistics;
using PersonalLogistics.Model;
using PersonalLogistics.ModPlayer;
using PersonalLogistics.Nebula.Packets;
using PersonalLogistics.SerDe;
using PersonalLogistics.Util;
using UnityEngine;
using static PersonalLogistics.Util.Log;
using static PersonalLogistics.Util.Constant;

namespace PersonalLogistics.Shipping
{
    public class ShippingManager : InstanceSerializer
    {
        private readonly Dictionary<Guid, Cost> _costs = new();
        private ItemBuffer _itemBuffer;
        private readonly TimeSpan _minAge = TimeSpan.FromSeconds(20);
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


        public void Import(BinaryReader r)
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
                    if (requestFromPlm == null)
                    {
                        requestFromPlm = requestsFromPlm.Find(req =>
                            req.ItemId == itemRequest.ItemId && req.ItemCount == itemRequest.ItemCount && req.RequestType == itemRequest.RequestType);
                    }

                    if (requestFromPlm != null)
                    {
                        itemRequest = requestFromPlm;
                        Debug($"replaced shipping mgr item request with instance from PLM {itemRequest.guid}");
                    }
                    else
                    {
                        Warn($"failed to replace shipping manager item request with actual from PLM. {itemRequest}");
                        continue;
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
                    if (cost.stationGid == 0)
                    {
                        Debug($"trying to resolve station gid for station with planet={cost.planetId} {cost.stationId}");
                        cost.stationGid = LogisticsNetwork.FindStationGid(cost.planetId, cost.stationId);
                    }
                    if (cost.paid)
                    {
                        continue;
                    }

                    var supplyStationInfo = LogisticsNetwork.FindStation(cost.stationGid, cost.planetId, cost.stationId);

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
                            if (GetPlayer().inventoryManager.RemoveItemImmediately(Mecha.WARPER_ITEMID, cntToRemove, out _))
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

                    if (cost.energyCost > 0)
                    {
                        if (supplyStationInfo != null && !PluginConfig.useMechaEnergyOnly.Value)
                        {
                            var actualRemoved = StationStorageManager.RemoveEnergyFromStation(supplyStationInfo, cost.energyCost);
                            if (actualRemoved >= cost.energyCost)
                            {
                                cost.energyCost = 0;
                            }
                            else
                            {
                                cost.energyCost -= actualRemoved;
                            }
                        }

                        if (cost.energyCost > 0 && !PluginConfig.neverUseMechaEnergy.Value)
                        {
                            // maybe we can use mecha energy instead
                            float ratio = GetPlayer().QueryEnergy(cost.energyCost);
                            if (ratio > 0.10)
                            {
                                var energyToUse = cost.energyCost * ratio;
                                GetPlayer().UseEnergy(energyToUse, Mecha.EC_DRONE);
                                var ratioInt = (int)(ratio * 100);
                                LogPopupWithFrequency($"Personal logistics using {{0}} ({{1}}% of needed) from mecha energy while retrieving item {itemRequest.ItemName}",
                                    energyToUse, ratioInt);
                                cost.energyCost -= (long)energyToUse;
                            }
                        }
                        else if (cost.energyCost > 0)
                        {
                            if (cost.processingPassesCompleted > 5)
                            {
                                Debug($"Trying to find another station to use energy from for {cost.energyCost}");
                                var otherStationsOnPlanet = LogisticsNetwork.stations.FindAll(st =>
                                    st.StationId != cost.stationId && st.PlanetInfo.PlanetId == cost.planetId);

                                foreach (var stationInfo in otherStationsOnPlanet)
                                {
                                    if (cost.energyCost <= 0)
                                        continue;
                                    // don't remove from PLS if request was made to ILS
                                    if (stationInfo.StationType != supplyStationInfo.StationType)
                                        continue;
                                    var actualRemoved = StationStorageManager.RemoveEnergyFromStation(stationInfo, cost.energyCost);
                                    if (actualRemoved >= cost.energyCost)
                                    {
                                        cost.energyCost = 0;
                                    }
                                    else
                                    {
                                        cost.energyCost -= actualRemoved;
                                    }
                                }
                            }

                            if (cost.energyCost > 0 && cost.processingPassesCompleted > 20)
                            {
                                LogAndPopupMessage($"Canceling request for {itemRequest.ItemName} after multiple failures to obtain energy cost");
                                GetPlayer().personalLogisticManager.CancelInboundRequests(itemRequest.guid.ToString());
                            }
                        }
                    }

                    if (cost.energyCost <= 0 && !cost.needWarper)
                    {
                        cost.paid = true;
                        cost.paidTick = GameMain.gameTick;
                    }
                    else
                    {
                        // since we are waiting on shipping but the cost isn't paid yet, need to advance completion time
                        // var computedTransitTime = itemRequest.ComputedCompletionTick - itemRequest.CreatedTick;
                        var stationInfo = LogisticsNetwork.FindStation(cost.stationGid, cost.planetId, cost.stationId);
                        if (stationInfo == null)
                        {
                            Warn($"Shipping manager did not find station by planet: {cost.planetId} {cost.stationId}");
                        }
                        else
                        {
                            var pos = GetPlayer().GetPosition();
                            var distance = StationStorageManager.GetDistance(pos.clusterPosition, pos.planetPosition, stationInfo);
                            var newArrivalTime = ShippingCostCalculator.CalculateArrivalTime(distance, stationInfo);
                            if (!ShippingCostCalculator.UseWarper(distance, stationInfo))
                            {
                                cost.needWarper = false;
                            }

                            itemRequest.ComputedCompletionTime = newArrivalTime;
                            var totalSeconds = (itemRequest.ComputedCompletionTime - DateTime.Now).TotalSeconds;
                            itemRequest.ComputedCompletionTick = GameMain.gameTick + TimeUtil.GetGameTicksFromSeconds(Mathf.CeilToInt((float)totalSeconds));
                            Debug($"Advancing item request completion time to {itemRequest.ComputedCompletionTime} due to unpaid cost ({itemRequest.ComputedCompletionTick})");
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
                if (desiredAmount.minDesiredAmount == 0 || !desiredAmount.allowBuffer || desiredAmount.maxDesiredAmount == 0)
                {
                    var remainingAmount = LogisticsNetwork.AddItem(pos.clusterPosition, inventoryItem.itemId, inventoryItem.ToItemStack());

                    _itemBuffer.UpsertItem(inventoryItem.itemId, remainingAmount);
                }
                else if (inventoryItem.count > GameMain.history.logisticShipCarries)
                {
                    ItemStack totalBufferAmount = inventoryItem.ToItemStack();
                    var amountToRemove = totalBufferAmount.Remove(inventoryItem.count - GameMain.history.logisticShipCarries);
                    var remaining = LogisticsNetwork.AddItem(pos.clusterPosition, inventoryItem.itemId, amountToRemove);
                    _itemBuffer.UpsertItem(inventoryItem.itemId, remaining);
                }
            }
        }


        private bool IsOldEnough(InventoryItem inventoryItem) => TimeSpan.FromSeconds(inventoryItem.AgeInSeconds) > _minAge;

        public ItemStack RemoveFromBuffer(int itemId, int count)
        {
            if (!_itemBuffer.HasItem(itemId))
            {
                return ItemStack.Empty();
            }

            var removedStack = _itemBuffer.RemoveItemCount(itemId, count);

            return removedStack;
        }

        public bool AddRequest(VectorLF3 playerUPosition, Vector3 playerLocalPosition, ItemRequest itemRequest)
        {
            var shipCapacity = GameMain.history.logisticShipCarries;
            var ramount = Math.Max(itemRequest.ItemCount, shipCapacity);
            if (shipCapacity < itemRequest.ItemCount)
            {
                // special case that can happen for Foundation that has stack size of 1k, but unresearched vessels can carry only 200 
                ramount = shipCapacity;
            }

            var (distance, removed, stationInfo) = LogisticsNetwork.RemoveItem(playerUPosition, playerLocalPosition, itemRequest.ItemId, ramount);
#if DEBUG
            if (PluginConfig.overriddenTransitTimeSeconds.Value > 0.001 && removed.ItemCount < ramount)
            {
                removed = ItemStack.FromCountAndPoints(ramount, ramount * 2);
            }
#endif
            if (removed.ItemCount == 0)
            {
                return false;
            }

            itemRequest.ComputedCompletionTime = ShippingCostCalculator.CalculateArrivalTime(distance, stationInfo);
            var totalSeconds = (itemRequest.ComputedCompletionTime - DateTime.Now).TotalSeconds;
            itemRequest.ComputedCompletionTick = GameMain.gameTick + TimeUtil.GetGameTicksFromSeconds(Mathf.CeilToInt((float)totalSeconds));
            if (totalSeconds > PluginConfig.maxWaitTimeInSeconds.Value)
            {
                LogPopupWithFrequency("Item: {0} arrival time is {1} seconds in future (more than configurable threshold of {2}), canceling request",
                    itemRequest.ItemName, totalSeconds, PluginConfig.maxWaitTimeInSeconds.Value);
                LogisticsNetwork.AddItem(playerUPosition, itemRequest.ItemId, removed);
                return false;
            }

            var addToBuffer = AddToBuffer(itemRequest.ItemId, removed);
            var actualBufferedItemCount = GetActualBufferedItemCount(itemRequest.ItemId);
            Debug($"Added {removed.ItemCount}, {removed.ProliferatorPoints} of item to buffer {actualBufferedItemCount}");
            if (!addToBuffer)
            {
                Warn($"Failed to add inbound items to storage buffer {itemRequest.ItemId} {itemRequest.State}");
                LogisticsNetwork.AddItem(playerUPosition, itemRequest.ItemId, removed);
            }

            if (itemRequest.ItemId == DEBUG_ITEM_ID)
            {
                Debug(
                    $"arrival time for {itemRequest.ItemId} is {itemRequest.ComputedCompletionTime} {ItemUtil.GetItemName(itemRequest.ItemId)} ticks {itemRequest.ComputedCompletionTick - GameMain.gameTick}");
            }

            // update task to reflect amount that we actually have
            itemRequest.ItemCount = Math.Min(removed.ItemCount, itemRequest.ItemCount);
            _requests.Enqueue(itemRequest);
            _requestByGuid[itemRequest.guid] = itemRequest;
            _costs.Add(itemRequest.guid, CalculateCost(distance, stationInfo, removed.ItemCount));
            return true;
        }

#if DEBUG
        public bool AddTestRequest(ItemRequest itemRequest, Cost testCost)
        {
            var addToBuffer = AddToBuffer(itemRequest.ItemId, itemRequest.ItemStack());

            if (itemRequest.ItemId == DEBUG_ITEM_ID)
            {
                Debug(
                    $"arrival time for {itemRequest.ItemId} is {itemRequest.ComputedCompletionTime} {ItemUtil.GetItemName(itemRequest.ItemId)} ticks {itemRequest.ComputedCompletionTick - GameMain.gameTick}");
            }

            // update task to reflect amount that we actually have
            _requests.Enqueue(itemRequest);
            _requestByGuid[itemRequest.guid] = itemRequest;
            _costs.Add(itemRequest.guid, testCost);
            return true;
        }
#endif

        private static Cost CalculateCost(double distance, StationInfo stationInfo, int actualShippingAmount)
        {
            var (energyCost, warperNeeded) = StationStorageManager.CalculateTripEnergyCost(stationInfo, distance);
            return new Cost
            {
                energyCost = energyCost * 2,
                needWarper = warperNeeded,
                planetId = stationInfo.PlanetInfo.PlanetId,
                stationId = stationInfo.StationId,
                shippingToBufferCount = actualShippingAmount
            };
        }

        public void MarkItemRequestFailed(Guid requestGuid)
        {
            if (_requestByGuid.TryGetValue(requestGuid, out var request))
            {
                request.State = RequestState.Failed;
            }
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

        public int GetBufferedItemCount(int itemId)
        {
            if (HasInProgressRequest(itemId))
            {
                return 0;
            }

            return _itemBuffer.GetItemCount(itemId);
        }

        public int GetActualBufferedItemCount(int itemId) => _itemBuffer.GetItemCount(itemId);

        public List<InventoryItem> GetDisplayableBufferedItems()
        {
            return _itemBuffer.GetInventoryItemView()
                .FindAll(invItem => !HasInProgressRequest(invItem.itemId));
        }

        private bool HasInProgressRequest(int itemId)
        {
            return _requestByGuid.Values.ToList().FindAll(itm => itm.ItemId == itemId)
                .Exists(itm => itm.State == RequestState.WaitingForShipping || itm.State == RequestState.ReadyForInventoryUpdate);
        }

        public void MoveBufferedItemToInventory(InventoryItem item)
        {
            if (!_itemBuffer.HasItem(item.itemId))
            {
                Warn($"Tried to remove item {item.itemName} from buffer into inventory but failed Instance==null");
                return;
            }

            if (HasInProgressRequest(item.itemId))
            {
                LogAndPopupMessage("Not sending item back to inventory until all in-progress requests are completed");
                return;
            }

            var removedFromBuffer = RemoveFromBuffer(item.itemId, item.count);
            if (removedFromBuffer.ItemCount < 1)
            {
                Warn($"did not actually remove any of {item.itemName} from buffer");
                return;
            }

            var remaining = GetPlayer().inventoryManager.AddItemToInventory(item.itemId, removedFromBuffer);

            if (remaining.ItemCount > 0)
            {
                Warn($"Removed {item.itemName} from buffer but failed to add all to inventory {remaining.ItemCount} not added");
                AddToBuffer(item.itemId, remaining);
            }
        }

        public int MoveAllBufferedItemsToLogisticsSystem(bool overrideDisplayable = false)
        {
            int stillInBufferCount = 0;
            var items = !overrideDisplayable ? GetDisplayableBufferedItems() : _itemBuffer.GetInventoryItemView();
            foreach (var item in items)
            {
                stillInBufferCount += MoveBufferedItemToLogisticsSystem(item);
            }

            return stillInBufferCount;
        }

        public int MoveBufferedItemToLogisticsSystem(InventoryItem item)
        {
            if (!_itemBuffer.HasItem(item.itemId))
            {
                return 0;
            }

            if (HasInProgressRequest(item.itemId))
            {
                LogAndPopupMessage("Not sending item back to logistics network until all in-progress requests are completed");
                return 0;
            }

            var moved = LogisticsNetwork.AddItem(GetPlayer().GetPosition().clusterPosition, item.itemId, item.ToItemStack());
            if (moved.ItemCount == 0)
            {
                _itemBuffer.RemoveItemCount(item.itemId, item.count);
                return 0;
            }

            item.count -= moved.ItemCount;
            return item.count;
        }

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

        public override string SummarizeState() => $"SM: {_itemBuffer.Count} bufferedItems, {_playerId}, {_costs.Count} costs, {_requests.Count} requests";

        public void UpsertBufferedItem(int itemId, int newItemCount, long gameTickUpdated, int packetProliferatorPoints)
        {
            _itemBuffer.UpsertItem(itemId, ItemStack.FromCountAndPoints(newItemCount, packetProliferatorPoints), gameTickUpdated);
        }

#if DEBUG
        public void ClearBuffer()
        {
            _itemBuffer.Clear();
        }
#endif
        public void AddRemoteRequest(VectorLF3 playerUPosition, Vector3 playerPosition, ItemRequest itemRequest)
        {
            var shipCapacity = GameMain.history.logisticShipCarries;
            var ramount = Math.Max(itemRequest.ItemCount, shipCapacity);
            if (shipCapacity < itemRequest.ItemCount)
            {
                // special case that can happen for Foundation that has stack size of 1k, but unresearched vessels can carry only 200 
                ramount = shipCapacity;
            }

            NebulaModAPI.MultiplayerSession.Network.SendPacket(new RemoveFromNetworkRequest(PlogPlayerRegistry.LocalPlayer().playerId.ToString(),
                itemRequest.guid.ToString(), playerUPosition, playerPosition, itemRequest.ItemId, ramount));
        }

        public void CompleteRemoteRequestRemove(RemoveFromNetworkResponse packet)
        {
            var itemRequest = GetPlayer().personalLogisticManager.GetRequests().Find(r => r.guid == Guid.Parse(packet.requestGuid));
            if (itemRequest == null)
            {
                Warn($"Did not find original request. {packet.requestGuid} {packet.distance}");
                return;
            }

            if (packet.removedCount == 0)
            {
                itemRequest.State = RequestState.Failed;
                return;
            }

            var stationInfo = LogisticsNetwork.FindStation(packet.stationGid, packet.planetId, packet.stationId);

            itemRequest.ComputedCompletionTime = ShippingCostCalculator.CalculateArrivalTime(packet.distance, stationInfo);
            var totalSeconds = (itemRequest.ComputedCompletionTime - DateTime.Now).TotalSeconds;
            itemRequest.ComputedCompletionTick = GameMain.gameTick + TimeUtil.GetGameTicksFromSeconds(Mathf.CeilToInt((float)totalSeconds));
            if (totalSeconds > PluginConfig.maxWaitTimeInSeconds.Value)
            {
                LogPopupWithFrequency("Item: {0} arrival time is {1} seconds in future (more than configurable threshold of {2}), canceling request",
                    itemRequest.ItemName, totalSeconds, PluginConfig.maxWaitTimeInSeconds.Value);
                // TODO send packet to host
                // LogisticsNetwork.AddItem(playerUPosition, itemRequest.ItemId, removed);
                itemRequest.State = RequestState.Failed;
            }

            var addToBuffer = AddToBuffer(itemRequest.ItemId, ItemStack.FromCountAndPoints(packet.removedCount, packet.removedAcc));
            var actualBufferedItemCount = GetActualBufferedItemCount(itemRequest.ItemId);
            Debug($"Added {packet.removedCount}, {packet.removedAcc} of item to buffer {actualBufferedItemCount}");
            if (!addToBuffer)
            {
                Warn($"Failed to add inbound items to storage buffer {itemRequest.ItemId} {itemRequest.State}");
                // TODO send packet to host
                return;
                // LogisticsNetwork.AddItem(playerUPosition, itemRequest.ItemId, removed);
            }

            if (itemRequest.ItemId == DEBUG_ITEM_ID)
            {
                Debug(
                    $"arrival time for {itemRequest.ItemId} is {itemRequest.ComputedCompletionTime} {ItemUtil.GetItemName(itemRequest.ItemId)} ticks {itemRequest.ComputedCompletionTick - GameMain.gameTick}");
            }
            // update task to reflect amount that we actually have
            itemRequest.ItemCount = Math.Min(packet.removedCount, itemRequest.ItemCount);
            _requests?.Enqueue(itemRequest);
            _requestByGuid[itemRequest.guid] = itemRequest;
            var cost = new Cost
            {
                energyCost = packet.tripEnergyCost * 2,
                needWarper = packet.warperNeeded,
                planetId = packet.planetId,
                stationId = packet.stationId,
                shippingToBufferCount = packet.removedCount
            };
            _costs.Add(itemRequest.guid, cost);
            itemRequest.State = RequestState.WaitingForShipping;
        }

        public void CompleteRemoteAdd(AddToNetworkResponse packet)
        {
            Debug($"Completing remote add of {packet.itemId}. {packet.remainingCount} items remain after doing add");
            var remainingAmount = ItemStack.FromCountAndPoints(packet.remainingCount, packet.remainingProliferatorPoints);
            _itemBuffer.Add(packet.itemId, remainingAmount, true);           
        }
    }
}