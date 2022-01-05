using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PersonalLogistics.Logistics;
using PersonalLogistics.Model;
using PersonalLogistics.PlayerInventory;
using PersonalLogistics.State;
using PersonalLogistics.Util;
using UnityEngine;
using static PersonalLogistics.Util.Log;
using static PersonalLogistics.Util.Constant;

namespace PersonalLogistics.Shipping
{
    public class ShippingManager : IPlayerContext
    {
        private readonly Dictionary<Guid, Cost> _costs = new();
        private readonly ItemBuffer _itemBuffer;
        private readonly TimeSpan _minAge = TimeSpan.FromSeconds(15);
        private readonly Dictionary<Guid, ItemRequest> _requestByGuid = new();
        private readonly Queue<ItemRequest> _requests = new();
        private bool _loadedFromImport;

        private readonly PLOGPlayer _player;
        // private static ShippingManager _instance;

        private ShippingManager(ItemBuffer loadItemBuffer)
        {
            _itemBuffer = loadItemBuffer;
        }

        public static ShippingManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    InitOnLoad();
                }

                return _instance;
            }
            private set => _instance = value;
        }

        public static void Export(BinaryWriter w)
        {
            if (Instance?._itemBuffer == null)
            {
                Warn("Trying to save while null shipping manager instance is present");
                return;
            }

            Instance._itemBuffer.Export(w);
            Debug($"wrote {Instance._itemBuffer.inventoryItems.Count} buffered items");
            var itemRequests = new List<ItemRequest>(Instance._requests)
                .FindAll(ir => ir.State != RequestState.Complete && ir.State != RequestState.Failed);
            w.Write(itemRequests.Count);
            for (var i = 0; i < itemRequests.Count; i++)
            {
                if (itemRequests[i].FromRecycleArea)
                    continue;
                itemRequests[i].Export(w);
            }

            var costTuples = new List<(Guid guid, Cost cost)>();
            foreach (var cost in Instance._costs)
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

        public static void Import(BinaryReader r)
        {
            try
            {
                Debug($"reading shipping manager data");

                var itemBuffer = ItemBuffer.Import(r);
                Instance = new ShippingManager(itemBuffer);
                Instance._loadedFromImport = true;
                var requestCount = r.ReadInt32();
                var requestsFromPlm = PersonalLogisticManager.Instance?.GetRequests() ?? new List<ItemRequest>();
                for (var i = requestCount - 1; i >= 0; i--)
                {
                    var itemRequest = ItemRequest.Import(r);
                    var requestFromPlm = requestsFromPlm.Find(req => req.guid == itemRequest.guid);
                    if (requestFromPlm != null)
                    {
                        itemRequest = requestFromPlm;
                    }
                    else
                    {
                        Warn($"failed to replace shipping manager item request with actual from PLM. {itemRequest}");
                    }

                    Instance._requests.Enqueue(itemRequest);
                    Instance._requestByGuid[itemRequest.guid] = itemRequest;
                }

                var costCount = r.ReadInt32();
                for (var i = 0; i < costCount; i++)
                {
                    var costGuid = Guid.Parse(r.ReadString());
                    var cost = Cost.Import(r);
                    Instance._costs[costGuid] = cost;
                }

                Debug($"Shipping manager read in {requestCount} requests and {costCount} costs");
            }
            catch (Exception e)
            {
                Warn("failed to Import shipping state");
                if (Instance == null)
                {
                    InitOnLoad();
                }
            }
        }

        public static void Save()
        {
            if (Instance == null)
            {
                return;
            }

            if (Instance._loadedFromImport)
            {
                return;
            }

            ShippingStatePersistence.SaveState(Instance._itemBuffer);
        }

        public static bool AddToBuffer(int itemId, int itemCount)
        {
            if (Instance == null)
            {
                throw new Exception("Shipping manager not initialized, unable to add item to buffer");
            }

            return Instance.Add(itemId, itemCount);
        }

        private bool Add(int itemId, int itemCount)
        {
            InventoryItem invItem;
            if (_itemBuffer.inventoryItemLookup.ContainsKey(itemId))
            {
                invItem = _itemBuffer.inventoryItemLookup[itemId];
            }
            else
            {
                invItem = new InventoryItem(itemId);
                _itemBuffer.inventoryItemLookup[itemId] = invItem;
                _itemBuffer.inventoryItems.Add(invItem);
            }

            if (invItem.count > 100_000)
            {
                Warn($"No more storage available for item {invItem.itemName}");
                return false;
            }

            invItem.LastUpdated = GameMain.gameTick;
            invItem.count += itemCount;
            Save();
            return true;
        }

        public static void Process()
        {
            var plogPlayers = PLOGPlayerRegistry.GetAllPlayers();
            foreach (var player in plogPlayers)
            {
                player.shippingManager.ProcessImpl();
            }
            // Instance?.ProcessImpl();
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

                    var stationComponent = LogisticsNetwork.stations.FirstOrDefault(st => st.stationId == cost.stationId && st.PlanetInfo.PlanetId == cost.planetId);

                    if (cost.needWarper)
                    {
                        if (stationComponent != null)
                        {
                            if (StationStorageManager.RemoveWarperFromStation(stationComponent))
                            {
                                cost.needWarper = false;
                            }
                        }

                        if (cost.needWarper)
                        {
                            // get from player 
                            if (InventoryManager.instance.RemoveItemImmediately(Mecha.WARPER_ITEMID, 1))
                            {
                                cost.needWarper = false;
                                LogPopupWithFrequency("Personal logistics removed warper from player inventory");
                            }
                        }
                    }

                    if (cost.energyCost > 0)
                    {
                        if (stationComponent != null && !PluginConfig.useMechaEnergyOnly.Value)
                        {
                            var actualRemoved = StationStorageManager.RemoveEnergyFromStation(stationComponent, cost.energyCost);
                            if (actualRemoved >= cost.energyCost)
                            {
                                cost.energyCost = 0;
                            }
                            else
                            {
                                cost.energyCost -= actualRemoved;
                            }
                        }

                        if (cost.energyCost > 0)
                        {
                            // maybe we can use mecha energy instead
                            // float ratio;
                            float ratio = _player.QueryEnergy(cost.energyCost);
                            // GameMain.mainPlayer.mecha.QueryEnergy(cost.energyCost, out var _, out ratio);
                            if (ratio > 0.10)
                            {
                                var energyToUse = cost.energyCost * ratio;
                                _player.UseEnergy(energyToUse, Mecha.EC_DRONE);
                                var ratioInt = (int)(ratio * 100);
                                LogPopupWithFrequency($"Personal logistics using {{0}} ({{1}}% of needed) from mecha energy while retrieving item {itemRequest.ItemName}",
                                    energyToUse, ratioInt);
                                cost.energyCost -= (long)energyToUse;
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
                        var byPlanetIdStationId = StationInfo.ByPlanetIdStationId(cost.planetId, cost.stationId);
                        if (byPlanetIdStationId == null) 
                        {
                            Warn($"Shipping manager did not find station by planet: {cost.planetId} {cost.stationId}");
                        }
                        else
                        {
                            var pos = _player.GetPosition();
                            var distance = StationStorageManager.GetDistance(pos.clusterPosition, pos.planetPosition, byPlanetIdStationId);
                            var newArrivalTime = CalculateArrivalTime(distance);
                            itemRequest.ComputedCompletionTime = newArrivalTime;
                            var totalSeconds = (itemRequest.ComputedCompletionTime - DateTime.Now).TotalSeconds;
                            itemRequest.ComputedCompletionTick = GameMain.gameTick + TimeUtil.GetGameTicksFromSeconds(Mathf.CeilToInt((float)totalSeconds));
                            Debug($"Advancing item request completion time to {itemRequest.ComputedCompletionTime} due to unpaid cost ({itemRequest.ComputedCompletionTick})");
                        }
                    }
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
            var pos = _player.GetPosition();
            var itemsToRemove = new List<InventoryItem>();
            foreach (var inventoryItem in _itemBuffer.inventoryItems)
            {
                if (!IsOldEnough(inventoryItem))
                {
                    continue;
                }

                var desiredAmount = InventoryManager.instance.GetDesiredAmount(inventoryItem.itemId);
                if (desiredAmount.minDesiredAmount == 0 || !desiredAmount.allowBuffer)
                {

                    var addedAmount = LogisticsNetwork.AddItem(pos.clusterPosition, inventoryItem.itemId, inventoryItem.count);
                    if (addedAmount == inventoryItem.count)
                    {
                        itemsToRemove.Add(inventoryItem);
                    }
                    else
                    {
                        inventoryItem.count -= addedAmount;
                    }
                }
                else if (inventoryItem.count > GameMain.history.logisticShipCarries)
                {
                    var amountToRemove = inventoryItem.count - GameMain.history.logisticShipCarries;
                    var addedAmount = LogisticsNetwork.AddItem(pos.clusterPosition, inventoryItem.itemId, amountToRemove);
                    inventoryItem.count -= addedAmount;
                }
            }

            foreach (var inventoryItem in itemsToRemove)
            {
                _itemBuffer.inventoryItems.Remove(inventoryItem);
                _itemBuffer.inventoryItemLookup.Remove(inventoryItem.itemId);
            }

            if (itemsToRemove.Count > 0)
            {
                Save();
            }
        }


        private bool IsOldEnough(InventoryItem inventoryItem) => new TimeSpan(inventoryItem.AgeInSeconds * 1000 * TimeSpan.TicksPerMillisecond) > _minAge;

        public static int RemoveFromBuffer(int itemId, int count)
        {
            if (Instance == null)
            {
                return 0;
            }

            return Instance.RemoveItemsFromBuffer(itemId, count);
        }

        public int RemoveItemsFromBuffer(int itemId, int count)
        {
            if (!_itemBuffer.inventoryItemLookup.ContainsKey(itemId))
            {
                return 0;
            }

            var inventoryItem = _itemBuffer.inventoryItemLookup[itemId];
            var removed = Math.Min(count, inventoryItem.count);
            inventoryItem.count -= removed;
            if (inventoryItem.count <= 0)
            {
                _itemBuffer.Remove(inventoryItem);
            }

            return removed;
        }

        public bool AddRequest(VectorLF3 playerPosition, Vector3 position, ItemRequest itemRequest)
        {
            // if (Instance == null)
            // {
            //     return false;
            // }
            //
            return AddRequestImpl(playerPosition, position, itemRequest);
        }

        private bool AddRequestImpl(VectorLF3 playerUPosition, Vector3 playerLocalPosition, ItemRequest itemRequest)
        {
            var shipCapacity = GameMain.history.logisticShipCarries;
            var ramount = Math.Max(itemRequest.ItemCount, shipCapacity);
            var actualRequestAmount = itemRequest.SkipBuffer ? itemRequest.ItemCount : ramount;
            if (itemRequest.fillBufferRequest)
            {
                actualRequestAmount = Math.Min(shipCapacity - GetBufferedItemCount(itemRequest.ItemId), actualRequestAmount);
            }

            (var distance, var removed, var stationInfo) = LogisticsNetwork.RemoveItem(playerUPosition, playerLocalPosition, itemRequest.ItemId, actualRequestAmount);
            if (removed == 0)
            {
                return false;
            }

            itemRequest.ComputedCompletionTime = CalculateArrivalTime(distance);
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
            itemRequest.ItemCount = Math.Min(removed, itemRequest.ItemCount);
            _requests.Enqueue(itemRequest);
            _requestByGuid[itemRequest.guid] = itemRequest;
            _costs.Add(itemRequest.guid, CalculateCost(distance, stationInfo));
            return true;
        }

        private static Cost CalculateCost(double distance, StationInfo stationInfo)
        {
            var sailSpeedModified = GameMain.history.logisticShipSailSpeedModified;
            var shipWarpSpeed = GameMain.history.logisticShipWarpDrive
                ? GameMain.history.logisticShipWarpSpeedModified
                : sailSpeedModified;
            var (energyCost, warperNeeded) = StationStorageManager.CalculateTripEnergyCost(stationInfo, distance, shipWarpSpeed);
            return new Cost
            {
                energyCost = energyCost * 2,
                needWarper = warperNeeded,
                planetId = stationInfo.PlanetInfo.PlanetId,
                stationId = stationInfo.stationId
            };
        }

        public static DateTime CalculateArrivalTime(double oneWayDistance)
        {
            var distance = oneWayDistance * 2;
            var droneSpeed = GameMain.history.logisticDroneSpeedModified;
            var interplanetaryShipSpeed = GameMain.history.logisticShipWarpDrive
                ? GameMain.history.logisticShipWarpSpeedModified
                : GameMain.history.logisticShipSailSpeedModified;
            if (distance > 5000)
            {
                // t = d/r
                var betweenPlanetsTransitTime = distance / interplanetaryShipSpeed;
                // transit time between planets plus a little extra to get to an actual spot on the planet
                return DateTime.Now.AddSeconds(betweenPlanetsTransitTime).AddSeconds(600 / droneSpeed);
            }

            // less than 5 km, we consider that to be on the same planet as us
            return DateTime.Now.AddSeconds(distance / droneSpeed);
        }

        public static bool ItemForTaskArrived(Guid requestGuid)
        {
            if (Instance == null)
            {
                throw new Exception("Shipping manager not initialized");
            }

            return Instance.ItemForTaskArrivedImpl(requestGuid);
        }

        private bool ItemForTaskArrivedImpl(Guid requestGuid)
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

        public static int GetBufferedItemCount(int itemId)
        {
            if (Instance == null)
            {
                return 0;
            }

            if (HasInProgressRequest(itemId))
            {
                return 0;
            }

            return Instance._itemBuffer.inventoryItemLookup.ContainsKey(itemId) ? Instance._itemBuffer.inventoryItemLookup[itemId].count : 0;
        }

        public int GetActualBufferedItemCount(int itemId) => _itemBuffer.inventoryItemLookup.ContainsKey(itemId) ? Instance._itemBuffer.inventoryItemLookup[itemId].count : 0;

        public static List<InventoryItem> GetDisplayableBufferedItems()
        {
            return new List<InventoryItem>(Instance._itemBuffer.inventoryItems
                .Where(invItem => !HasInProgressRequest(invItem.itemId)));
        }

        private static bool HasInProgressRequest(int itemId)
        {
            return Instance._requestByGuid.Values.ToList().FindAll(itm => itm.ItemId == itemId)
                .Exists(itm => itm.State == RequestState.WaitingForShipping || itm.State == RequestState.ReadyForInventoryUpdate);
        }

        public void MoveBufferedItemToInventory(InventoryItem item)
        {
            if (!_itemBuffer.inventoryItemLookup.ContainsKey(item.itemId) && InventoryManager.instance == null)
            {
                Warn($"Tried to remove item {item.itemName} from buffer into inventory but failed Instance==null = {InventoryManager.instance == null}");
                return;
            }

            if (HasInProgressRequest(item.itemId))
            {
                LogAndPopupMessage("Not sending item back to inventory until all in-progress requests are completed");
                return;
            }

            var removedFromBuffer = RemoveItemsFromBuffer(item.itemId, item.count);
            if (removedFromBuffer < 1)
            {
                Warn($"did not actually remove any of {item.itemName} from buffer");
                return;
            }

            var movedCount = InventoryManager.instance.AddItemToInventory(item.itemId, removedFromBuffer);

            if (movedCount < removedFromBuffer)
            {
                Warn($"Removed {item.itemName} from buffer but failed to add all to inventory {movedCount} actually added");
                Add(item.itemId, removedFromBuffer - movedCount);
            }
        }

        public void MoveAllBufferedItemsToLogisticsSystem()
        {
            var items = GetDisplayableBufferedItems();
            foreach (var item in items)
            {
                MoveBufferedItemToLogisticsSystem(item);
            }

            Save();
        }

        public void MoveBufferedItemToLogisticsSystem(InventoryItem item)
        {
            if (!_itemBuffer.inventoryItemLookup.ContainsKey(item.itemId))
            {
                return;
            }

            if (HasInProgressRequest(item.itemId))
            {
                LogAndPopupMessage("Not sending item back to logistics network until all in-progress requests are completed");
                return;
            }

            var moved = LogisticsNetwork.AddItem(_player.GetPosition().clusterPosition, item.itemId, item.count);
            if (moved == item.count)
            {
                _itemBuffer.Remove(item);
            }
            else
            {
                item.count -= moved;
            }
        }

        public static void Reset()
        {
            Save();
            Instance = null;
        }

        public static void InitOnLoad()
        {
            if (ShippingStatePersistence.LegacyExternalSaveExists(GameUtil.GetSeedInt()))
            {
                var loadState = ShippingStatePersistence.LoadState(GameUtil.GetSeedInt());
                if (loadState.inventoryItems == null)
                {
                    loadState.inventoryItems = new List<InventoryItem>();
                }

                if (loadState.inventoryItemLookup == null)
                {
                    loadState.inventoryItemLookup = new Dictionary<int, InventoryItem>();
                    foreach (var inventoryItem in loadState.inventoryItems)
                    {
                        loadState.inventoryItemLookup[inventoryItem.itemId] = inventoryItem;
                    }
                }

                Instance = new ShippingManager(loadState)
                {
                    _loadedFromImport = false
                };
            }
            else
            {
                var itemBuffer = new ItemBuffer
                {
                    seed = GameUtil.GetSeedInt(),
                    inventoryItems = new List<InventoryItem>(),
                    inventoryItemLookup = new Dictionary<int, InventoryItem>()
                };
                Instance = new ShippingManager(itemBuffer)
                {
                    _loadedFromImport = true
                };
            }
        }

        public static ShippingManager InstanceFor()
        {
            return new ShippingManager(new ItemBuffer());
        }

        public PLOGPlayerId GetPlayerId()
        {
            return _player.playerId;
        }
    }
}