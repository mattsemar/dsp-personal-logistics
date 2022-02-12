using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Timers;
using PersonalLogistics.Model;
using PersonalLogistics.ModPlayer;
using PersonalLogistics.Util;
using UnityEngine;
using static PersonalLogistics.Util.Log;

namespace PersonalLogistics.Logistics
{
    public class StationProductInfo
    {
        public int ItemCount;
        public int ItemId;
        public int ProliferatorPoints;
    }

    public enum StationType
    {
        PLS,
        ILS
    }

    public class PlanetInfo
    {
        public VectorLF3 lastLocation;
        public string Name;
        public int PlanetId;
    }

    public class StationInfo
    {
        private static readonly ConcurrentDictionary<int, ConcurrentDictionary<int, StationInfo>> pool = new();
        private readonly StationProductInfo[] _products = new StationProductInfo[15];

        public Vector3 LocalPosition;
        public PlanetInfo PlanetInfo;
        public string PlanetName;
        public int StationId;
        public bool IsOrbitalCollector;
        public StationType StationType;

        private readonly StationProductInfo[] _localExports = new StationProductInfo[15];
        private readonly StationProductInfo[] _remoteExports = new StationProductInfo[15];

        private readonly StationProductInfo[] _requestedItems = new StationProductInfo[15];
        private readonly StationProductInfo[] _suppliedItems = new StationProductInfo[15];
        private readonly ConcurrentDictionary<int, int> _itemToIndex = new();
        private readonly ConcurrentDictionary<int, int> _indexToItem = new();

        public List<StationProductInfo> Products
        {
            get
            {
                var result = new List<StationProductInfo>();
                for (int i = 0; i < _products.Length; i++)
                {
                    var stationProductInfo = _products[i];
                    if (stationProductInfo == null)
                        continue;
                    result.Add(stationProductInfo);
                }

                return result;
            }
        }

        public static StationInfo Build(StationComponent station, PlanetData planet)
        {
            if (!pool.TryGetValue(planet.id, out var planetPool) || planetPool == null)
            {
                planetPool = new ConcurrentDictionary<int, StationInfo>();
                pool[planet.id] = planetPool;
            }

            if (!planetPool.TryGetValue(station.id, out var stationInfo))
            {
                stationInfo = new StationInfo
                {
                    PlanetName = planet.displayName,
                    StationType = station.isStellar ? StationType.ILS : StationType.PLS,
                    StationId = station.id,
                    IsOrbitalCollector = station.isCollector
                };
                planetPool[station.id] = stationInfo;
            }

            stationInfo.PlanetInfo = new PlanetInfo
            {
                lastLocation = planet.uPosition,
                Name = planet.displayName,
                PlanetId = planet.id
            };
            stationInfo.LocalPosition = station.shipDockPos;

            for (int i = 0; i < station.storage.Length; i++)
            {
                var store = station.storage[i];
                if (store.itemId < 1)
                {
                    if (stationInfo._indexToItem.ContainsKey(i))
                    {
                        var oldItemId = stationInfo._indexToItem[i];
                        stationInfo._indexToItem.TryRemove(i, out _);
                        stationInfo._itemToIndex.TryRemove(oldItemId, out _);
                    }

                    continue;
                }

                stationInfo._indexToItem[i] = store.itemId;
                stationInfo._itemToIndex[store.itemId] = i;

                var productInfo = new StationProductInfo
                {
                    ItemId = store.itemId,
                    ItemCount = store.count,
                    ProliferatorPoints = store.inc
                };
                stationInfo._products[i] = productInfo;

                if (store.totalOrdered < 0)
                {
                    // these are already spoken for so take them from total
                    productInfo.ItemCount = Math.Max(0, productInfo.ItemCount + store.totalOrdered);
                }

                var isSupply = false;
                bool isDemand = store.remoteLogic == ELogisticStorage.Demand;

                if (store.remoteLogic == ELogisticStorage.Supply)
                {
                    isSupply = true;
                    stationInfo._remoteExports[i] = productInfo;
                }
                else
                {
                    stationInfo._remoteExports[i] = null;
                }

                if (store.localLogic == ELogisticStorage.Supply)
                {
                    isSupply = true;
                    stationInfo._localExports[i] = productInfo;
                }
                else
                {
                    stationInfo._localExports[i] = null;
                }

                if (store.localLogic == ELogisticStorage.Demand)
                {
                    isDemand = true;
                }

                stationInfo._suppliedItems[i] = null;
                stationInfo._requestedItems[i] = null;
                if (isSupply)
                {
                    if (productInfo.ItemCount > 0)
                    {
                        stationInfo._suppliedItems[i] = productInfo;
                    }
                }

                if (isDemand)
                {
                    stationInfo._requestedItems[i] = productInfo;
                }
            }


            return stationInfo;
        }


        public bool HasItem(int itemId) => _itemToIndex.ContainsKey(itemId);

        public static StationInfo ByPlanetIdStationId(int planetId, int stationId)
        {
            if (!pool.TryGetValue(planetId, out var stationsByPlanet))
            {
                Warn($"Failed to load planetary stations for {planetId} {pool.Count}");
            }
            else
            {
                if (!stationsByPlanet.TryGetValue(stationId, out var stationInfo))
                {
                    Warn($"Failed to load station {stationId} from planet pool {stationsByPlanet.Count}");
                }
                else
                {
                    return stationInfo;
                }
            }

            Warn($"Trying to get station from components {planetId} {stationId} {pool.Count}");
            var stationAndPlanet = StationStorageManager.GetStationComp(planetId, stationId);
            if (stationAndPlanet.station == null || stationAndPlanet.planet == null)
            {
                Warn($"2nd attempt failed get station {planetId} {stationId}");
                return null;
            }

            return Build(stationAndPlanet.station, stationAndPlanet.planet);
        }

        // use this for testing
        public static StationInfo GetAnyStationWithItem(int itemId)
        {
            lock (pool)
            {
                foreach (var planetId in pool.Keys)
                {
                    foreach (var stationInfo in pool[planetId].Values)
                    {
                        if (stationInfo.HasItem(itemId))
                        {
                            return stationInfo;
                        }
                    }
                }
            }

            return null;
        }

        public bool HasAnyExport(int itemId)
        {
            if (_itemToIndex.TryGetValue(itemId, out int index))
            {
                return (_localExports[index] != null && _localExports[index].ItemCount > 0)
                       || (_remoteExports[index] != null && _remoteExports[index].ItemCount > 0);
            }

            return false;
        }

        public bool HasLocalExport(int itemId)
        {
            if (_itemToIndex.TryGetValue(itemId, out int index))
            {
                return _localExports[index] != null && _localExports[index].ItemCount > 0;
            }

            return false;
        }

        public bool HasRemoteExport(int itemId)
        {
            if (_itemToIndex.TryGetValue(itemId, out int index))
            {
                return _remoteExports[index] != null && _remoteExports[index].ItemCount > 0;
            }

            return false;
        }

        public StationProductInfo GetProductInfo(int itemId)
        {
            if (_itemToIndex.TryGetValue(itemId, out int index))
            {
                return _products[index];
            }

            return null;
        }

        public bool IsSupplied(int itemId)
        {
            if (_itemToIndex.TryGetValue(itemId, out int index))
            {
                return _suppliedItems[index] != null && _suppliedItems[index].ItemCount > 0;
            }

            return false;
        }

        public bool IsRequested(int itemId)
        {
            if (_itemToIndex.TryGetValue(itemId, out int index))
            {
                return _requestedItems[index] != null;
            }

            return false;
        }
    }

    public class ByItemSummary
    {
        public int SuppliedLocally;
    }

    public static class LogisticsNetwork
    {
        private static readonly List<StationInfo> _stations = new();
        private static readonly ConcurrentDictionary<int, ByItemSummary> byItemSummary = new();
        public static bool IsInitted;
        public static bool IsRunning;
        public static bool IsFirstLoadComplete;
        private static Timer _timer;


        public static List<StationInfo> stations
        {
            get
            {
                lock (_stations)
                {
                    return _stations;
                }
            }
        }

        public static void Start()
        {
            _timer = new Timer(6_000);
            _timer.Elapsed += DoPeriodicTask;
            _timer.AutoReset = true;
            _timer.Enabled = true;
            IsInitted = true;
        }

        private static void DoPeriodicTask(object source, ElapsedEventArgs e)
        {
            try
            {
                if (PluginConfig.IsPaused())
                {
                    return;
                }

                CollectStationInfos(source, e);
            }
            catch (Exception exc)
            {
                Warn($"exception in periodic task {exc.Message}\n{exc.StackTrace}");
            }
        }

        public static ByItemSummary ForItemId(int itemId)
        {
            byItemSummary.TryGetValue(itemId, out var summary);
            return summary;
        }

        private static void CollectStationInfos(object source, ElapsedEventArgs e)
        {
            if (IsRunning)
            {
                logger.LogWarning("Collect already running");
                return;
            }

            var localPlanetId = GameMain.localPlanet?.id ?? 0;
            IsRunning = true;
            var newStations = new List<StationInfo>();
            var newByItemSummary = new Dictionary<int, ByItemSummary>();
            try
            {
                foreach (var star in GameMain.universeSimulator.galaxyData.stars)
                {
                    foreach (var planet in star.planets)
                    {
                        if (planet.factory != null && planet.factory.factorySystem != null &&
                            planet.factory.transport != null &&
                            planet.factory.transport.stationCursor != 0)
                        {
                            var transport = planet.factory.transport;
                            var isLocalPlanet = PlogPlayerRegistry.IsLocalPlayerPlanet(planet.id);
                            for (var i = 1; i < transport.stationCursor; i++)
                            {
                                var station = transport.stationPool[i];
                                if (station == null || station.id != i)
                                {
                                    continue;
                                }

                                var stationInfo = StationInfo.Build(station, planet);
                                newStations.Add(stationInfo);
                                foreach (var productInfo in stationInfo.Products)
                                {
                                    if (productInfo == null)
                                        continue;

                                    var isSupply = localPlanetId == stationInfo.PlanetInfo.PlanetId || stationInfo.StationType == StationType.ILS;
                                    var suppliedLocallyCount = isLocalPlanet && stationInfo.HasAnyExport(productInfo.ItemId) ? productInfo.ItemCount : 0;
                                    if (newByItemSummary.TryGetValue(productInfo.ItemId, out var summary))
                                    {
                                        if (stationInfo.IsSupplied(productInfo.ItemId))
                                        {
                                            if (isSupply)
                                            {
                                            }
                                        }
                                        else
                                        {
                                        }

                                        summary.SuppliedLocally += suppliedLocallyCount;
                                    }
                                    else
                                    {
                                        newByItemSummary[productInfo.ItemId] = new ByItemSummary
                                        {
                                            SuppliedLocally = suppliedLocallyCount
                                        };
                                    }
                                }
                            }
                        }
                    }
                }

                IsInitted = true;
            }
            catch (Exception err)
            {
                logger.LogWarning($"Collection task failed {err}");
            }
            finally
            {
                lock (_stations)
                {
                    _stations.Clear();
                    _stations.AddRange(newStations);
                }

                byItemSummary.Clear();
                foreach (var itemId in newByItemSummary.Keys)
                {
                    byItemSummary.TryAdd(itemId, newByItemSummary[itemId]);
                }

                IsRunning = false;
                IsFirstLoadComplete = true;
            }
        }

        public static void Stop()
        {
            IsInitted = false;
            IsFirstLoadComplete = false;
            if (_timer == null)
            {
                return;
            }

            _timer.Stop();
            _timer.Dispose();
        }

        public static bool HasItem(int itemId) => byItemSummary.ContainsKey(itemId);

        public static bool StationCanSupply(VectorLF3 playerUPosition, Vector3 playerLocalPosition, int itemId, StationInfo stationInfo)
        {
            if (!stationInfo.HasItem(itemId))
            {
                return false;
            }

            if (stationInfo.IsOrbitalCollector && PluginConfig.neverUseMechaEnergy.Value)
                return false;

            // Any station with item is eligible
            var stationOnSamePlanet = StationStorageManager.GetDistance(playerUPosition, playerLocalPosition, stationInfo) < 600;

            switch (PluginConfig.cheatLevel.Value)
            {
                // this case should not actually be used
                case CheatLevel.Full:
                    return true;
                case CheatLevel.Quarter:
                    return stationInfo.IsSupplied(itemId);
                case CheatLevel.Planetary:
                {
                    return stationOnSamePlanet && stationInfo.HasAnyExport(itemId);
                }
                case CheatLevel.Half:
                {
                    return true;
                }
            }

            Warn($"unhandled source mode, should not reach here. {PluginConfig.cheatLevel.Value}");
            return false;
        }

        public static (double distance, ItemStack removed, StationInfo stationInfo) RemoveItem(VectorLF3 playerUPosition, Vector3 playerLocalPosition, int itemId,
            int itemCount)
        {
            var (totalAvailable, stationsWithItem) =
                CountTotalAvailable(itemId, new PlogPlayerPosition { clusterPosition = playerUPosition, planetPosition = playerLocalPosition });
            if (totalAvailable == 0)
            {
                Debug($"total available for {itemId} is 0. Found {stationsWithItem.Count}");
                return (0, ItemStack.FromCountAndPoints(0, 0), null);
            }

            if (PluginConfig.minStacksToLoadFromStations.Value > 0)
            {
                int stacksAvailable = ItemUtil.CalculateStacksFromItemCount(itemId, totalAvailable);
                if (stacksAvailable < PluginConfig.minStacksToLoadFromStations.Value)
                {
                    LogPopupWithFrequency("{0} has only {1} stacks available in network, not removing. Config set to minimum of {2}",
                        ItemUtil.GetItemName(itemId), stacksAvailable, PluginConfig.minStacksToLoadFromStations.Value);
                    return (0, ItemStack.FromCountAndPoints(0, 0), null);
                }
            }

            stationsWithItem.Sort((s1, s2) =>
            {
                var s1Distance = StationStorageManager.GetDistance(playerUPosition, playerLocalPosition, s1);
                var s2Distance = StationStorageManager.GetDistance(playerUPosition, playerLocalPosition, s2);
                return s1Distance.CompareTo(s2Distance);
            });
            var removedAmount = ItemStack.FromCountAndPoints(0, 0);
            var distance = -1.0d;
            StationInfo stationPayingCost = null;
            int stationPayingCostSuppliedAmount = 0;
            while (removedAmount.ItemCount < itemCount && stationsWithItem.Count > 0)
            {
                var stationInfo = stationsWithItem[0];
                stationsWithItem.RemoveAt(0);
                var removeResult = StationStorageManager.RemoveFromStation(stationInfo, itemId, itemCount - removedAmount.ItemCount);

                if (removeResult.ItemCount > 0)
                {
                    Debug(
                        $"Removed {removeResult.ItemCount}, inc={removeResult.ProliferatorPoints} of {ItemUtil.GetItemName(itemId)} from station on {stationInfo.PlanetName} for player inventory");
                }

                removedAmount.Add(removeResult);
                // the station we get the bulk of the items from pays the cost and is used to calculate distance
                if (removeResult.ItemCount > 0 && removeResult.ItemCount > stationPayingCostSuppliedAmount)
                {
                    distance = StationStorageManager.GetDistance(playerUPosition, playerLocalPosition, stationInfo);
                    stationPayingCost = stationInfo;
                    stationPayingCostSuppliedAmount = removeResult.ItemCount;
                }
            }

            var boostedStack = PluginConfig.BoostStackProliferator(removedAmount);
            return (distance, boostedStack, stationPayingCost);
        }

        /// <summary>
        /// Returns remaining items that were not added
        /// </summary>
        public static ItemStack AddItem(VectorLF3 playerUPosition, int itemId, ItemStack amountToAdd)
        {
            var stationsWithItem = stations.FindAll(s => s.HasItem(itemId));
            stationsWithItem.Sort((s1, s2) =>
            {
                var s1Distance = s1.PlanetInfo.lastLocation.Distance(playerUPosition);
                var s2Distance = s2.PlanetInfo.lastLocation.Distance(playerUPosition);
                return s1Distance.CompareTo(s2Distance);
            });
            var remainingItems = ItemStack.FromCountAndPoints(amountToAdd.ItemCount, amountToAdd.ProliferatorPoints);
            while (remainingItems.ItemCount > 0 && stationsWithItem.Count > 0)
            {
                var stationInfo = stationsWithItem[0];
                stationsWithItem.RemoveAt(0);
                var addedCount = StationStorageManager.AddToStation(stationInfo, itemId, remainingItems);
                Debug(
                    $"Added {addedCount} of {ItemUtil.GetItemName(itemId)} to station {stationInfo.StationId} on {stationInfo.PlanetName} remaining acc: {remainingItems.ProliferatorPoints}, remaining items: {remainingItems.ItemCount}");
            }

            if (remainingItems.ItemCount > 0)
            {
                Warn(
                    $"Added less than requested amount of {ItemUtil.GetItemName(itemId)} to stations. Added amount: {amountToAdd.ItemCount - remainingItems.ItemCount}, requested: {amountToAdd.ItemCount}");
            }

            return remainingItems;
        }

        private static (int availableCount, List<StationInfo> matchedStations) CountTotalAvailable(int itemId, PlogPlayerPosition position = null)
        {
            var pos = position ?? new PlogPlayerPosition
            {
                clusterPosition = GameMain.mainPlayer.uPosition,
                planetPosition = GameMain.mainPlayer.position
            };

            var total = 0;
            var stationsWithItem = stations.FindAll(s =>
                s.IsSupplied(itemId) && StationCanSupply(pos.clusterPosition, pos.planetPosition, itemId, s));
            foreach (var stationInfo in stationsWithItem)
            {
                total += stationInfo.GetProductInfo(itemId)?.ItemCount ?? 0;
            }

            return (total, stationsWithItem);
        }

        /// <summary>
        /// Available on one of the stations on the current planet ILS (LocalSupply) / PLS with (Supply)  
        /// </summary>
        public static bool IsAvailableLocally(int itemId)
        {
            if (!byItemSummary.TryGetValue(itemId, out var summary))
            {
                return false;
            }

            return summary.SuppliedLocally > 0;
        }
    }
}