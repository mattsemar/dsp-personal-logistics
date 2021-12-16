using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Timers;
using PersonalLogistics.Model;
using PersonalLogistics.PlayerInventory;
using PersonalLogistics.Shipping;
using PersonalLogistics.Util;
using UnityEngine;
using static PersonalLogistics.Util.Log;

namespace PersonalLogistics.Logistics
{
    public class StationProductInfo
    {
        public int ItemCount;
        public int ItemId;
        public string ItemName;
        public int MaxCount;
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
        private static readonly Dictionary<int, Dictionary<int, StationInfo>> pool =
            new Dictionary<int, Dictionary<int, StationInfo>>();

        public readonly List<StationProductInfo> LocalExports = new List<StationProductInfo>();
        public readonly List<StationProductInfo> LocalImports = new List<StationProductInfo>();
        public readonly List<StationProductInfo> RemoteExports = new List<StationProductInfo>();
        public readonly List<StationProductInfo> RemoteImports = new List<StationProductInfo>();

        public readonly HashSet<int> RequestedItems = new HashSet<int>();
        public readonly HashSet<int> SuppliedItems = new HashSet<int>();
        public HashSet<int> ItemTypes = new HashSet<int>();
        public Vector3 localPosition;
        public PlanetInfo PlanetInfo;
        public string PlanetName;
        public List<StationProductInfo> Products = new List<StationProductInfo>();
        public int stationId;
        public StationType StationType;

        public static StationInfo Build(StationComponent station, PlanetData planet)
        {
            StationInfo stationInfo;
            if (pool.ContainsKey(planet.id) && pool[planet.id].ContainsKey(station.id))
            {
                stationInfo = pool[planet.id][station.id];
                stationInfo.ItemTypes.Clear();
                stationInfo.LocalExports.Clear();
                stationInfo.LocalImports.Clear();
                stationInfo.RemoteExports.Clear();
                stationInfo.RemoteImports.Clear();
                stationInfo.SuppliedItems.Clear();
                stationInfo.RequestedItems.Clear();
            }
            else
            {
                stationInfo = new StationInfo
                {
                    PlanetName = planet.displayName,
                    StationType = station.isStellar ? StationType.ILS : StationType.PLS
                };
            }

            foreach (var store in station.storage)
            {
                if (store.itemId < 1)
                {
                    continue;
                }

                var itemName = ItemUtil.GetItemName(store.itemId);
                var productInfo = new StationProductInfo
                {
                    ItemId = store.itemId,
                    ItemName = itemName,
                    ItemCount = store.count,
                    MaxCount = store.max
                };
                if (store.totalOrdered < 0)
                {
                    // these are already spoken for so take them from total
                    productInfo.ItemCount = Math.Max(0, productInfo.ItemCount + store.totalOrdered);
                }
                
                stationInfo.ItemTypes.Add(store.itemId);
                stationInfo.Products.Add(productInfo);
                var isSupply = false;
                var isDemand = false;
                if (store.remoteLogic == ELogisticStorage.Demand)
                {
                    isDemand = true;
                    stationInfo.RemoteImports.Add(productInfo);
                }

                if (store.remoteLogic == ELogisticStorage.Supply)
                {
                    isSupply = true;
                    stationInfo.RemoteExports.Add(productInfo);
                }

                if (store.localLogic == ELogisticStorage.Supply)
                {
                    isSupply = true;
                    stationInfo.LocalExports.Add(productInfo);
                }

                if (store.localLogic == ELogisticStorage.Demand)
                {
                    stationInfo.LocalImports.Add(productInfo);
                    isDemand = true;
                }

                if (isSupply)
                {
                    if (productInfo.ItemCount > 0)
                    {
                        stationInfo.SuppliedItems.Add(productInfo.ItemId);
                    }
                }

                if (isDemand)
                {
                    stationInfo.RequestedItems.Add(productInfo.ItemId);
                }
            }

            stationInfo.stationId = station.id;
            stationInfo.PlanetInfo = new PlanetInfo
                { lastLocation = planet.uPosition, Name = planet.displayName, PlanetId = planet.id };
            stationInfo.localPosition = station.shipDockPos;
            return stationInfo;
        }


        public bool HasItem(int itemId) => ItemTypes.Contains(itemId);
    }

    public class ByItemSummary
    {
        public int AvailableItems;
        public HashSet<int> PlanetIds = new HashSet<int>();
        public int Requesters;
        public int SuppliedItems;
        public int Suppliers;
        public int TotalStorage;
    }

    public static class LogisticsNetwork
    {
        private static readonly List<StationInfo> _stations = new List<StationInfo>();
        public static readonly ConcurrentDictionary<int, int> byItem = new ConcurrentDictionary<int, int>();
        public static readonly Dictionary<int, ByItemSummary> byItemSummary = new Dictionary<int, ByItemSummary>();
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
                    return new List<StationInfo>(_stations);
                }
            }
        }

        public static void Start()
        {
            _timer = new Timer(5_000);
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
            var newByItem = new Dictionary<int, int>();
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
                                    if (!newByItem.ContainsKey(productInfo.ItemId))
                                    {
                                        newByItem[productInfo.ItemId] = productInfo.ItemCount;
                                    }
                                    else
                                    {
                                        newByItem[productInfo.ItemId] += productInfo.ItemCount;
                                    }

                                    var isSupply = localPlanetId == stationInfo.PlanetInfo.PlanetId || stationInfo.StationType == StationType.ILS;
                                    if (newByItemSummary.TryGetValue(productInfo.ItemId, out var summary))
                                    {
                                        summary.AvailableItems += productInfo.ItemCount;
                                        summary.PlanetIds.Add(stationInfo.PlanetInfo.PlanetId);

                                        summary.TotalStorage += productInfo.MaxCount;
                                        if (stationInfo.SuppliedItems.Contains(productInfo.ItemId))
                                        {
                                            summary.Suppliers++;
                                            if (isSupply)
                                            {
                                                summary.SuppliedItems += productInfo.ItemCount;
                                            }
                                        }
                                        else
                                        {
                                            summary.Requesters++;
                                        }
                                    }
                                    else
                                    {
                                        newByItemSummary[productInfo.ItemId] = new ByItemSummary
                                        {
                                            AvailableItems = productInfo.ItemCount,
                                            Requesters = stationInfo.RequestedItems.Contains(productInfo.ItemId) ? 1 : 0,
                                            Suppliers = stationInfo.SuppliedItems.Contains(productInfo.ItemId) ? 1 : 0,
                                            TotalStorage = stationInfo.StationType == StationType.ILS ? 10000 : 5000, // TODO fix this to get real  value
                                            SuppliedItems = isSupply && stationInfo.SuppliedItems.Contains(productInfo.ItemId) ? productInfo.ItemCount : 0
                                        };
                                        newByItemSummary[productInfo.ItemId].PlanetIds.Add(stationInfo.PlanetInfo.PlanetId);
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

                lock (byItem)
                {
                    byItem.Clear();
                    foreach (var itemId in newByItem.Keys)
                    {
                        byItem[itemId] = newByItem[itemId];
                    }
                }

                lock (byItemSummary)
                {
                    byItemSummary.Clear();
                    foreach (var itemId in newByItemSummary.Keys)
                    {
                        byItemSummary[itemId] = newByItemSummary[itemId];
                    }
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

        public static bool HasItem(int itemId) => byItem.ContainsKey(itemId);

        public static bool IsItemSupplied(int itemId, Player player)
        {
            try
            {
                if (player == null)
                {
                    Warn("player is null can't check supply status");
                    return false;
                }

                return stations.Exists(s =>
                    s.SuppliedItems.Contains(itemId) && StationCanSupply(player.uPosition, player.position, itemId, s));
            }
            catch (Exception e)
            {
                Warn($"Got exception checking for item supply {e}\r\n {e.Message}\r\n{e.StackTrace}");
                return false;
            }
        }

        public static bool StationCanSupply(VectorLF3 playerUPosition, Vector3 playerLocalPosition, int itemId, StationInfo stationInfo)
        {
            if (!stationInfo.HasItem(itemId))
            {
                return false;
            }
            // Any station with item is eligible

            var stationOnSamePlanet = StationStorageManager.GetDistance(playerUPosition, playerLocalPosition, stationInfo) < 600;

            switch (PluginConfig.stationRequestMode.Value)
            {
                case StationSourceMode.All:
                    return true;
                case StationSourceMode.AnySupply:
                    return stationInfo.SuppliedItems.Contains(itemId);
                case StationSourceMode.IlsDemandRules:
                {
                    if (!stationInfo.SuppliedItems.Contains(itemId))
                    {
                        return false;
                    }

                    if (stationInfo.StationType == StationType.PLS)
                    {
                        // must be on same planet
                        return stationOnSamePlanet;
                    }

                    if (stationOnSamePlanet)
                    {
                        // must be set to local supply
                        if (stationInfo.LocalExports.Exists(pi => pi.ItemId == itemId))
                        {
                            return true;
                        }
                    }

                    return stationInfo.RemoteExports.Exists(pi => pi.ItemId == itemId);
                }
                case StationSourceMode.IlsDemandWithPls:
                {
                    if (!stationInfo.SuppliedItems.Contains(itemId))
                    {
                        return false;
                    }

                    if (stationInfo.StationType == StationType.PLS)
                    {
                        return stationInfo.LocalExports.Exists(pi => pi.ItemId == itemId);
                    }

                    if (stationOnSamePlanet)
                    {
                        // must be set to local supply
                        if (stationInfo.LocalExports.Exists(pi => pi.ItemId == itemId))
                        {
                            return true;
                        }
                    }

                    return stationInfo.RemoteExports.Exists(pi => pi.ItemId == itemId);
                }
            }

            Warn($"unhandled source mode, should not reach here. {PluginConfig.stationRequestMode.Value}");
            return false;
        }

        public static (double distance, int itemsRemoved, StationInfo stationInfo) RemoveItem(VectorLF3 playerUPosition, Vector3 playerLocalPosition, int itemId, int itemCount)
        {
            var stationsWithItem = stations.FindAll(s => StationCanSupply(playerUPosition, playerLocalPosition, itemId, s));
            stationsWithItem.Sort((s1, s2) =>
            {
                var s1Distance = StationStorageManager.GetDistance(playerUPosition, playerLocalPosition, s1);
                var s2Distance = StationStorageManager.GetDistance(playerUPosition, playerLocalPosition, s2);
                return s1Distance.CompareTo(s2Distance);
            });
            var removedItemCount = 0;
            var distance = -1.0d;
            StationInfo stationPayingCost = null;
            int stationPayingCostSuppliedAmount = 0;
            while (removedItemCount < itemCount && stationsWithItem.Count > 0)
            {
                var stationInfo = stationsWithItem[0];
                stationsWithItem.RemoveAt(0);
                var removedCount =
                    StationStorageManager.RemoveFromStation(stationInfo, itemId, itemCount - removedItemCount);

                if (removedCount > 0)
                {
                    Debug($"Removed {removedCount} of {ItemUtil.GetItemName(itemId)} from station on {stationInfo.PlanetName} for player inventory at {DateTime.Now}");
                }

                removedItemCount += removedCount;
                // the station we get the bulk of the items from pays the cost and is used to calculate distance
                if (removedCount > 0 && removedCount > stationPayingCostSuppliedAmount)
                {
                    distance = StationStorageManager.GetDistance(playerUPosition, playerLocalPosition, stationInfo);
                    stationPayingCost = stationInfo;
                    stationPayingCostSuppliedAmount = removedCount;
                }
            }

            return (distance, removedItemCount, stationPayingCost);
        }

        public static int AddItem(VectorLF3 playerUPosition, int itemId, int itemCount)
        {
            var stationsWithItem = stations.FindAll(s => s.HasItem(itemId));
            stationsWithItem.Sort((s1, s2) =>
            {
                var s1Distance = s1.PlanetInfo.lastLocation.Distance(playerUPosition);
                var s2Distance = s2.PlanetInfo.lastLocation.Distance(playerUPosition);
                return s1Distance.CompareTo(s2Distance);
            });
            var addedItemCount = 0;
            while (addedItemCount < itemCount && stationsWithItem.Count > 0)
            {
                var stationInfo = stationsWithItem[0];
                stationsWithItem.RemoveAt(0);
                var stationAddedAmount = itemCount - addedItemCount;
                var addedCount = StationStorageManager.AddToStation(stationInfo, itemId, stationAddedAmount);
                addedItemCount += addedCount;
                var stationProducts = string.Join(", ", stationInfo.Products.Select(s => s.ItemName));
                Debug(
                    $"Added {addedCount} of {ItemUtil.GetItemName(itemId)} to station {stationInfo.stationId} {stationProducts} on {stationInfo.PlanetName}");
            }

            if (addedItemCount < itemCount)
            {
                Warn(
                    $"Added less than requested amount of {ItemUtil.GetItemName(itemId)} to stations. Added amount: {addedItemCount}, requested: {itemCount}");
            }

            return addedItemCount;
        }

        public static string ItemSummary(int itemId)
        {
            if (!byItem.ContainsKey(itemId) || !byItemSummary.ContainsKey(itemId))
            {
                if (IsInitted && IsFirstLoadComplete)
                {
                    return "Not available in logistics network";
                }

                return "Personal logistics still loading...";
            }

            var stringBuilder = new StringBuilder();
            if (PluginConfig.stationRequestMode.Value == StationSourceMode.All)
            {
                stringBuilder.Append($"Supplied: {byItemSummary[itemId].SuppliedItems}\r\n");
            }
            else
            {
                var total = 0;
                var stationsWithItem = stations.FindAll(s =>
                    s.SuppliedItems.Contains(itemId) && StationCanSupply(GameMain.mainPlayer.uPosition, GameMain.mainPlayer.position, itemId, s));
                foreach (var stationInfo in stationsWithItem)
                {
                    var stationProductInfos = stationInfo.Products.FindAll(p => p.ItemId == itemId);
                    foreach (var productInfo in stationProductInfos)
                    {
                        total += productInfo.ItemCount;
                    }
                }

                stringBuilder.Append($"Supplied: {total}\r\n");
            }

            stringBuilder.Append($"Supply: {byItemSummary[itemId].Suppliers}, demand: {byItemSummary[itemId].Requesters}\r\n");
            stringBuilder.Append($"Total items: {byItemSummary[itemId].AvailableItems}\r\n");
            if (PersonalLogisticManager.Instance != null && PersonalLogisticManager.Instance.HasTaskForItem(itemId))
            {
                var itemRequest = PersonalLogisticManager.Instance.GetRequest(itemId);
                if (itemRequest != null && itemRequest.RequestType == RequestType.Load)
                {
                    stringBuilder.Append($"{itemRequest.ItemCount} requested\r\n");
                }
            }

            var bufferedAmount = ShippingManager.GetBufferedItemCount(itemId);

            stringBuilder.Append($"{bufferedAmount} in buffer\r\n");

            return stringBuilder.ToString();
        }

        public static string ShortItemSummary(int itemId)
        {
            if (!byItem.ContainsKey(itemId) || !byItemSummary.ContainsKey(itemId))
            {
                return "Not available in logistics network";
            }

            try
            {
                var stringBuilder = new StringBuilder($"Total items: {byItem[itemId]}\r\n");
                var stationsWithItem = stations.FindAll(s =>
                    s.SuppliedItems.Contains(itemId) && StationCanSupply(GameMain.mainPlayer.uPosition, GameMain.mainPlayer.position, itemId, s));

                if (PluginConfig.stationRequestMode.Value == StationSourceMode.All)
                {
                    stringBuilder.Append($"Supplied: {byItemSummary[itemId].SuppliedItems}\r\n");
                }
                else
                {
                    var total = 0;
                    foreach (var stationInfo in stationsWithItem)
                    {
                        var stationProductInfos = stationInfo.Products.FindAll(p => p.ItemId == itemId);
                        foreach (var productInfo in stationProductInfos)
                        {
                            total += productInfo.ItemCount;
                        }
                    }

                    stringBuilder.Append($"Supplied: {total}\r\n");
                }

                if (stationsWithItem.Count > 0)
                {
                    var stationInfos = stationsWithItem.FindAll(st => StationCanSupply(GameMain.mainPlayer.uPosition, GameMain.mainPlayer.position, itemId, st));
                    var enumerable = stationInfos
                        .Select(st => (StationStorageManager.GetDistance(GameMain.mainPlayer.uPosition, GameMain.mainPlayer.position, st), st));
                    var closest = long.MaxValue;
                    var closestStation = stationsWithItem.First();
                    foreach (var valueTuple in enumerable)
                    {
                        if (valueTuple.Item1 < closest)
                        {
                            closest = (long)valueTuple.Item1;
                            closestStation = valueTuple.st;
                        }
                    }

                    var calculateArrivalTime = ShippingManager.CalculateArrivalTime(closest);
                    var secondsAway = (int)(calculateArrivalTime - DateTime.Now).TotalSeconds;
                    stringBuilder.Append($"Closest {closest} meters (approx {secondsAway} seconds)");
                    if (closestStation != null)
                    {
                        stringBuilder.Append($" on {closestStation.PlanetInfo.Name}");
                    }
                }

                var bufferedItemCount = ShippingManager.GetBufferedItemCount(itemId);
                if (bufferedItemCount > 0)
                {
                    stringBuilder.Append($"\r\nBuffered: {bufferedItemCount}");
                }

                return stringBuilder.ToString();
            }
            catch (Exception e)
            {
                Warn($"still getting exception {e.Message} {e.StackTrace}");
                return "Personal logistics syncing";
            }
        }
    }
}