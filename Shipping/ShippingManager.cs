using System;
using System.Collections.Generic;
using System.Linq;
using PersonalLogistics.Logistics;
using PersonalLogistics.Model;
using PersonalLogistics.PlayerInventory;
using PersonalLogistics.StationStorage;
using PersonalLogistics.Util;
using static PersonalLogistics.Log;
using static PersonalLogistics.Util.Constant;

namespace PersonalLogistics.Shipping
{
    public class ShippingManager
    {
        private readonly ItemBuffer _itemBuffer;
        private readonly Queue<ItemRequest> _requests = new Queue<ItemRequest>();
        private readonly Dictionary<Guid, ItemRequest> _requestByGuid = new Dictionary<Guid, ItemRequest>();
        private readonly Dictionary<Guid, Cost> _costs = new Dictionary<Guid, Cost>();
        private readonly TimeSpan _minAge = TimeSpan.FromSeconds(15);
        private static DateTime _lastPopup = DateTime.Now;

        private static ShippingManager _instance;

        private ShippingManager(ItemBuffer loadItemBuffer)
        {
            _itemBuffer = loadItemBuffer;
        }

        public static ShippingManager Instance => _instance;


        public static void Init()
        {
            if (_instance != null && _instance._itemBuffer.seed == GameUtil.GetSeedInt())
                return;
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

            _instance = new ShippingManager(loadState);
            Save();
        }

        public static void Save()
        {
            if (_instance == null)
            {
                return;
            }

            ShippingStatePersistence.SaveState(_instance._itemBuffer);
        }

        public static bool AddToBuffer(int itemId, int itemCount)
        {
            if (_instance == null)
            {
                Init();
                if (_instance == null)
                    throw new Exception($"Shipping manager not initialized, unable to add item to buffer");
            }

            return _instance.Add(itemId, itemCount);
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
                invItem = new InventoryItem
                {
                    itemId = itemId,
                    itemName = ItemUtil.GetItemName(itemId)
                };
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
            ShippingStatePersistence.SaveState(_itemBuffer);
            return true;
        }

        public static void Process()
        {
            _instance?.ProcessImpl();
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


                if (_costs.TryGetValue(itemRequest.guid, out Cost cost))
                {
                    if (cost.paid)
                        continue;
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
                            if (InventoryManager.Instance.RemoveItemImmediately(Mecha.WARPER_ITEMID, 1))
                            {
                                cost.needWarper = false;
                                LogPopup($"Personal logistics removed warper from player inventory");
                            }
                        }
                    }

                    if (cost.energyCost > 0)
                    {
                        if (stationComponent != null && !PluginConfig.useMechaEnergyOnly.Value)
                        {
                            long actualRemoved = StationStorageManager.RemoveEnergyFromStation(stationComponent, cost.energyCost);
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
                            float ratio;
                            GameMain.mainPlayer.mecha.QueryEnergy(cost.energyCost, out var _, out ratio);
                            if (ratio > 0.99)
                            {
                                GameMain.mainPlayer.mecha.MarkEnergyChange(Mecha.EC_DRONE, -cost.energyCost);
                                GameMain.mainPlayer.mecha.UseEnergy(cost.energyCost);
                                LogPopup($"Personal logistics using {cost.energyCost} of mecha energy");
                                cost.energyCost -= cost.energyCost;
                            }
                            else if (ratio > 0.10)
                            {
                                var energyToUse = cost.energyCost * ratio;
                                GameMain.mainPlayer.mecha.MarkEnergyChange(Mecha.EC_DRONE, -energyToUse);
                                GameMain.mainPlayer.mecha.UseEnergy(energyToUse);
                                LogPopup($"Personal logistics using {energyToUse} of mecha energy");
                                cost.energyCost -= (long) energyToUse;
                            }
                        }
                    }

                    if (cost.energyCost <= 0 && !cost.needWarper)
                        cost.paid = true;
                }
            }

            var totalElapsed = new TimeSpan(DateTime.Now.Ticks - startTicks);
            if (totalElapsed.Milliseconds < 250)
            {
                SendBufferedItemsToNetwork();
            }

            Debug($"Shipping completed after {totalElapsed.Milliseconds} ms");
        }

        private void SendBufferedItemsToNetwork()
        {
            var itemsToRemove = new List<InventoryItem>();
            foreach (var inventoryItem in _itemBuffer.inventoryItems)
            {
                if (!IsOldEnough(inventoryItem))
                    continue;
                var desiredAmount = InventoryManager.Instance.GetDesiredAmount(inventoryItem.itemId);
                if (desiredAmount.minDesiredAmount == 0 || !desiredAmount.allowBuffer)
                {
                    var addedAmount = LogisticsNetwork.AddItem(GameMain.mainPlayer.uPosition, inventoryItem.itemId, inventoryItem.count);
                    if (addedAmount == inventoryItem.count)
                        itemsToRemove.Add(inventoryItem);
                    else
                        inventoryItem.count -= addedAmount;
                }
                else if (inventoryItem.count > GameMain.history.logisticShipCarries)
                {
                    var amountToRemove = inventoryItem.count - GameMain.history.logisticShipCarries;
                    var addedAmount = LogisticsNetwork.AddItem(GameMain.mainPlayer.uPosition, inventoryItem.itemId, amountToRemove);
                    inventoryItem.count -= addedAmount;
                }
            }

            foreach (var inventoryItem in itemsToRemove)
            {
                _itemBuffer.inventoryItems.Remove(inventoryItem);
                _itemBuffer.inventoryItemLookup.Remove(inventoryItem.itemId);
            }

            if (itemsToRemove.Count > 0)
                ShippingStatePersistence.SaveState(_itemBuffer);
        }


        private bool IsOldEnough(InventoryItem inventoryItem)
        {
            return new TimeSpan(inventoryItem.AgeInSeconds * 1000 * TimeSpan.TicksPerMillisecond) > _minAge;
        }

        public static int RemoveFromBuffer(int itemId, int count)
        {
            if (_instance == null)
                return 0;
            return _instance.RemoveItemsFromBuffer(itemId, count);
        }

        public int RemoveItemsFromBuffer(int itemId, int count)
        {
            if (!_itemBuffer.inventoryItemLookup.ContainsKey(itemId))
                return 0;
            var inventoryItem = _itemBuffer.inventoryItemLookup[itemId];
            var removed = Math.Min(count, inventoryItem.count);
            inventoryItem.count -= removed;
            if (inventoryItem.count <= 0)
            {
                _itemBuffer.Remove(inventoryItem);
            }

            return removed;
        }

        public static bool AddRequest(VectorLF3 playerPosition, ItemRequest itemRequest)
        {
            if (_instance == null)
            {
                Init();
                if (_instance == null)
                    return false;
            }

            return _instance.AddRequestImpl(playerPosition, itemRequest);
        }

        private bool AddRequestImpl(VectorLF3 playerPosition, ItemRequest itemRequest)
        {
            var shipCapacity = GameMain.history.logisticShipCarries;
            var ramount = Math.Max(itemRequest.ItemCount, shipCapacity);
            var actualRequestAmount = itemRequest.SkipBuffer ? itemRequest.ItemCount : ramount;
            (double distance, int removed, var stationInfo) = LogisticsNetwork.RemoveItem(playerPosition, itemRequest.ItemId, actualRequestAmount);
            if (removed == 0)
            {
                return false;
            }

            itemRequest.ComputedCompletionTime = CalculateArrivalTime(distance);
            itemRequest.ComputedCompletionTick = GameMain.gameTick + (long)((itemRequest.ComputedCompletionTime - DateTime.Now).TotalSeconds * 60);  

            var addToBuffer = AddToBuffer(itemRequest.ItemId, removed);
            if (!addToBuffer)
            {
                Warn($"Failed to add inbound items to storage buffer {itemRequest.ItemId} {itemRequest.State}");
                LogisticsNetwork.AddItem(playerPosition, itemRequest.ItemId, removed);
            }

            if (itemRequest.ItemId == DEBUG_ITEM_ID)
                Debug($"arrival time for {itemRequest.ItemId} is {itemRequest.ComputedCompletionTime} {ItemUtil.GetItemName(itemRequest.ItemId)} ticks {itemRequest.ComputedCompletionTick - GameMain.gameTick}");
            // update task to reflect amount that we actually have
            itemRequest.ItemCount = Math.Min(removed, itemRequest.ItemCount);
            _requests.Enqueue(itemRequest);
            _requestByGuid[itemRequest.guid] = itemRequest;
            _costs.Add(itemRequest.guid, CalculateCost(distance, stationInfo));
            return true;
        }

        private Cost CalculateCost(double distance, StationInfo stationInfo)
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
            var sailSpeedModified = GameMain.history.logisticShipSailSpeedModified;
            var shipWarpSpeed = GameMain.history.logisticShipWarpDrive
                ? GameMain.history.logisticShipWarpSpeedModified
                : sailSpeedModified;
            if (distance > 5000)
            {
                // d=rt
                // t = d/r
                var timeToArrival = distance / shipWarpSpeed;
                return DateTime.Now.AddSeconds(timeToArrival).AddSeconds(5000 / sailSpeedModified);
            }

            return DateTime.Now.AddSeconds(distance / sailSpeedModified);
        }

        public static bool ItemForTaskArrived(Guid requestGuid)
        {
            if (_instance == null)
            {
                throw new Exception($"Shipping manager not initialized");
            }

            return _instance.ItemForTaskArrivedImpl(requestGuid);
        }

        private bool ItemForTaskArrivedImpl(Guid requestGuid)
        {
            if (_requestByGuid.TryGetValue(requestGuid, out ItemRequest request))
            {
                if (_costs.TryGetValue(requestGuid, out Cost cost))
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

        public static int GetBufferedItemCount(int itemId)
        {
            if (_instance == null)
            {
                return 0;
            }

            return _instance._itemBuffer.inventoryItemLookup.ContainsKey(itemId) ? _instance._itemBuffer.inventoryItemLookup[itemId].count : 0;
        }

        public static List<InventoryItem> GetInventoryItems()
        {
            return new List<InventoryItem>(_instance._itemBuffer.inventoryItems);
        }

        public void MoveBufferedItemToInventory(InventoryItem item)
        {
            if (!_itemBuffer.inventoryItemLookup.ContainsKey(item.itemId) && InventoryManager.Instance == null)
            {
                Warn($"Tried to remove item {item.itemName} from buffer into inventory but failed Instance==null = {InventoryManager.Instance == null}");
                return;
            }

            var removedFromBuffer = RemoveItemsFromBuffer(item.itemId, item.count);
            if (removedFromBuffer < 1)
            {
                Warn($"did not actually remove any of {item.itemName} from buffer");
                return;
            }

            var movedCount = InventoryManager.Instance.AddItemToInventory(item.itemId, removedFromBuffer);

            if (movedCount < removedFromBuffer)
            {
                Warn($"Removed {item.itemName} from buffer but failed to add all to inventory {movedCount} actually added");
                Add(item.itemId, removedFromBuffer - movedCount);
            }
        }

        public void MoveBufferedItemToLogisticsSystem(InventoryItem item)
        {
            if (!_itemBuffer.inventoryItemLookup.ContainsKey(item.itemId))
            {
                return;
            }

            var moved = LogisticsNetwork.AddItem(GameMain.mainPlayer.uPosition, item.itemId, item.count);
            if (moved == item.count)
                _itemBuffer.Remove(item);
            else
                item.count -= moved;
        }

        public static void Reset()
        {
            Save();
            _instance = null;
        }

        private static void LogPopup(string msg)
        {
            if (new TimeSpan(DateTime.Now.Ticks - _lastPopup.Ticks).TotalMinutes < 2)
            {
                Debug($"(popup suppressed) {msg}");
                return;
            }

            _lastPopup = DateTime.Now;
            LogAndPopupMessage(msg);
        }
    }
}