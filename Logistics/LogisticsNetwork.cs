using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Timers;
using PersonalLogistics.Model;
using PersonalLogistics.ModPlayer;
using PersonalLogistics.Nebula;
using PersonalLogistics.Nebula.Client;
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
        public int ProliferatorPoints;

        public void Export(BinaryWriter w)
        {
            w.Write(ItemCount);
            w.Write(ItemId);
            w.Write(ProliferatorPoints);
        }

        public static StationProductInfo Import(BinaryReader r)
        {
            return new StationProductInfo
            {
                ItemCount = r.ReadInt32(),
                ItemId = r.ReadInt32(),
                ProliferatorPoints = r.ReadInt32()
            };
        }
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

        public void Export(BinaryWriter w)
        {
            w.Write(PlanetId);
            w.Write(Name);
            w.Write(lastLocation.x);
            w.Write(lastLocation.y);
            w.Write(lastLocation.z);
        }

        public static PlanetInfo Import(BinaryReader r)
        {
            return new PlanetInfo
            {
                PlanetId = r.ReadInt32(),
                Name = r.ReadString(),
                lastLocation = new VectorLF3(r.ReadDouble(), r.ReadDouble(), r.ReadDouble())
            };
        }
    }

    public class ByItemSummary
    {
        public int AvailableItems;
        public int Requesters;
        public int SuppliedItems;
        public int Suppliers;
        public int SuppliedLocally;
        public int ProliferatorPoints;

        public static ByItemSummary operator -(ByItemSummary a, ByItemSummary b)
        {
            return new ByItemSummary
            {
                AvailableItems = a.AvailableItems - b.AvailableItems,
                Requesters = a.Requesters - b.Requesters,
                SuppliedItems = a.SuppliedItems - b.SuppliedItems,
                Suppliers = a.Suppliers - b.Suppliers,
                SuppliedLocally = a.SuppliedLocally - b.SuppliedLocally,
                ProliferatorPoints = a.ProliferatorPoints - b.ProliferatorPoints
            };
        }
        public static ByItemSummary operator +(ByItemSummary a, ByItemSummary b)
        {
            return new ByItemSummary
            {
                AvailableItems = a.AvailableItems + b.AvailableItems,
                Requesters = a.Requesters + b.Requesters,
                SuppliedItems = a.SuppliedItems + b.SuppliedItems,
                Suppliers = a.Suppliers + b.Suppliers,
                SuppliedLocally = a.SuppliedLocally + b.SuppliedLocally,
                ProliferatorPoints = a.ProliferatorPoints + b.ProliferatorPoints
            };
        }
            

    }

    public static class LogisticsNetwork
    {
        private static readonly List<StationInfo> _stations = new();
        private static readonly ConcurrentDictionary<int, StationInfo> _stationByGid = new();
        private static readonly ConcurrentDictionary<int, ByItemSummary> byItemSummary = new();
        public static bool IsInitted;
        public static bool IsRunning;
        public static bool IsFirstLoadComplete;
        private static Timer _timer;
        private static StringBuilder _toolTipAmountsSb = new("          ", 10);


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

                if (NebulaLoadState.IsMultiplayerClient())
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

                                var (stationInfo, changed) = StationInfo.Build(station, planet);
                                newStations.Add(stationInfo);
                                _stationByGid.TryAdd(stationInfo.StationGid, stationInfo);
                                if (changed && NebulaLoadState.IsMultiplayerHost())
                                {
                                    RequestClient.NotifyStationInfo(stationInfo);
                                }
                                else
                                {
                                    if (!changed && NebulaLoadState.IsMultiplayerHost())
                                    {
                                        Debug($"Station {stationInfo.StationId} did not change");
                                    }
                                }

                                foreach (var productInfo in stationInfo.Products)
                                {
                                    if (productInfo == null)
                                        continue;

                                    var isSupply = localPlanetId == stationInfo.PlanetInfo.PlanetId ||
                                                   stationInfo.StationType == StationType.ILS;
                                    var suppliedLocallyCount =
                                        isLocalPlanet && stationInfo.HasAnyExport(productInfo.ItemId)
                                            ? productInfo.ItemCount
                                            : 0;
                                    if (newByItemSummary.TryGetValue(productInfo.ItemId, out var summary))
                                    {
                                        summary.AvailableItems += productInfo.ItemCount;
                                        summary.ProliferatorPoints += productInfo.ProliferatorPoints;

                                        if (stationInfo.IsSupplied(productInfo.ItemId))
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

                                        summary.SuppliedLocally += suppliedLocallyCount;
                                    }
                                    else
                                    {
                                        newByItemSummary[productInfo.ItemId] = new ByItemSummary
                                        {
                                            AvailableItems = productInfo.ItemCount,
                                            Requesters = stationInfo.IsRequested(productInfo.ItemId) ? 1 : 0,
                                            Suppliers = stationInfo.IsSupplied(productInfo.ItemId) ? 1 : 0,
                                            SuppliedItems = isSupply && stationInfo.IsSupplied(productInfo.ItemId)
                                                ? productInfo.ItemCount
                                                : 0,
                                            SuppliedLocally = suppliedLocallyCount,
                                            ProliferatorPoints = productInfo.ProliferatorPoints
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

        public static bool StationCanSupply(VectorLF3 playerUPosition, Vector3 playerLocalPosition, int itemId,
            StationInfo stationInfo)
        {
            if (!stationInfo.HasItem(itemId))
            {
                return false;
            }

            if (stationInfo.IsOrbitalCollector && PluginConfig.neverUseMechaEnergy.Value)
                return false;

            // Any station with item is eligible
            var stationOnSamePlanet =
                StationStorageManager.GetDistance(playerUPosition, playerLocalPosition, stationInfo) < 600;

            switch (PluginConfig.stationRequestMode.Value)
            {
                case StationSourceMode.All:
                    return true;
                case StationSourceMode.AnySupply:
                    return stationInfo.IsSupplied(itemId);
                case StationSourceMode.Planetary:
                {
                    return stationOnSamePlanet && stationInfo.HasAnyExport(itemId);
                }
                case StationSourceMode.IlsDemandRules:
                {
                    if (!stationInfo.IsSupplied(itemId))
                    {
                        return false;
                    }

                    if (stationInfo.StationType == StationType.PLS && stationInfo.HasLocalExport(itemId))
                    {
                        // must be on same planet
                        return stationOnSamePlanet;
                    }

                    if (stationOnSamePlanet)
                    {
                        // must be set to local supply
                        if (stationInfo.HasLocalExport(itemId))
                        {
                            return true;
                        }
                    }

                    return stationInfo.HasRemoteExport(itemId);
                }
                case StationSourceMode.IlsDemandWithPls:
                {
                    if (!stationInfo.IsSupplied(itemId))
                    {
                        return false;
                    }

                    if (stationInfo.StationType == StationType.PLS)
                    {
                        return stationInfo.HasLocalExport(itemId);
                    }

                    if (stationOnSamePlanet)
                    {
                        // must be set to local supply
                        if (stationInfo.HasLocalExport(itemId))
                        {
                            return true;
                        }
                    }

                    return stationInfo.HasRemoteExport(itemId);
                }
            }

            Warn($"unhandled source mode, should not reach here. {PluginConfig.stationRequestMode.Value}");
            return false;
        }

        public static (double distance, ItemStack removed, StationInfo stationInfo) RemoveItem(
            VectorLF3 playerUPosition, Vector3 playerLocalPosition, int itemId,
            int itemCount)
        {
            var (totalAvailable, stationsWithItem) =
                CountTotalAvailable(itemId,
                    new PlogPlayerPosition {clusterPosition = playerUPosition, planetPosition = playerLocalPosition});
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
                    LogPopupWithFrequency(
                        "{0} has only {1} stacks available in network, not removing. Config set to minimum of {2}",
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
                var removeResult =
                    StationStorageManager.RemoveFromStation(stationInfo, itemId, itemCount - removedAmount.ItemCount);

                if (removeResult.ItemCount > 0)
                {
                    Debug(
                        $"Removed {removeResult.ItemCount}, inc={removeResult.ProliferatorPoints} of {ItemUtil.GetItemName(itemId)} from station on {stationInfo.PlanetName} for player inventory");
#if DEBUG
                    Warn($"{JsonUtility.ToJson(stationInfo, true)}");
#endif
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

            return (distance, removedAmount, stationPayingCost);
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

        public static (string summaryTxt, bool hitNull) ItemSummary(int itemId)
        {
            bool hitNull = !byItemSummary.TryGetValue(itemId, out var byItemSumm);

            if (hitNull)
            {
                if (IsInitted && IsFirstLoadComplete)
                {
                    return ("Not available in logistics network", true);
                }

                return ("Personal logistics still loading...", true);
            }

            var stringBuilder = new StringBuilder();
            if (PluginConfig.stationRequestMode.Value == StationSourceMode.All)
            {
                StringBuilderUtility.WriteKMG(_toolTipAmountsSb, 8, byItemSumm.SuppliedItems);
                stringBuilder.Append($"Supplied: {_toolTipAmountsSb}\r\n");
            }
            else if (PluginConfig.stationRequestMode.Value == StationSourceMode.Planetary)
            {
                stringBuilder.Append($"Available: {byItemSumm.SuppliedLocally}\r\n");
            }
            else
            {
                var (total, _) = CountTotalAvailable(itemId);
                StringBuilderUtility.WriteKMG(_toolTipAmountsSb, 8, total);
                stringBuilder.Append($"Available: {_toolTipAmountsSb}\r\n");
            }

            stringBuilder.Append($"Suppliers: {byItemSumm.Suppliers}, requesters: {byItemSumm.Requesters}\r\n");
            StringBuilderUtility.WriteKMG(_toolTipAmountsSb, 8, byItemSumm.AvailableItems);
            stringBuilder.Append($"Total items: {_toolTipAmountsSb}\r\n");

            var proliferatorPoints = byItemSumm.ProliferatorPoints;

            StringBuilderUtility.WriteKMG(_toolTipAmountsSb, 8, proliferatorPoints);
            stringBuilder.Append("增产点数共计".Translate());
            stringBuilder.Append($"{_toolTipAmountsSb}\r\n");
            var bufferedAmount = PlogPlayerRegistry.LocalPlayer().shippingManager.GetBufferedItemCount(itemId);
            stringBuilder.Append($"{bufferedAmount} in buffer\r\n");

            return (stringBuilder.ToString(), false);
        }

        private static (int availableCount, List<StationInfo> matchedStations) CountTotalAvailable(int itemId,
            PlogPlayerPosition position = null)
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

        public static string ShortItemSummary(int itemId)
        {
            bool hitNull = !byItemSummary.TryGetValue(itemId, out var byItemSumm);

            if (hitNull)
            {
                if (IsInitted && IsFirstLoadComplete)
                {
                    return "Not available in logistics network";
                }

                return "Personal logistics still loading...";
            }

            try
            {
                var stringBuilder = new StringBuilder($"Total items: {byItemSumm.AvailableItems}\r\n");
                var stationsWithItem = stations.FindAll(s =>
                    s.IsSupplied(itemId) && StationCanSupply(GameMain.mainPlayer.uPosition,
                        GameMain.mainPlayer.position, itemId, s));

                if (PluginConfig.stationRequestMode.Value == StationSourceMode.All)
                {
                    stringBuilder.Append($"Supplied: {byItemSumm.SuppliedItems}\r\n");
                }
                else
                {
                    var total = stationsWithItem.Sum(stationInfo => stationInfo.GetProductInfo(itemId)?.ItemCount ?? 0);

                    stringBuilder.Append($"Supplied: {total}\r\n");
                }

                if (stationsWithItem.Count > 0)
                {
                    var stationInfos = stationsWithItem.FindAll(st =>
                        StationCanSupply(GameMain.mainPlayer.uPosition, GameMain.mainPlayer.position, itemId, st));
                    var enumerable = stationInfos
                        .Select(st => (
                            StationStorageManager.GetDistance(GameMain.mainPlayer.uPosition,
                                GameMain.mainPlayer.position, st), st));
                    var closest = long.MaxValue;
                    var closestStation = stationsWithItem.First();
                    foreach (var valueTuple in enumerable)
                    {
                        if (valueTuple.Item1 < closest)
                        {
                            closest = (long) valueTuple.Item1;
                            closestStation = valueTuple.st;
                        }
                    }

                    var calculateArrivalTime = ShippingCostCalculator.CalculateArrivalTime(closest, closestStation);
                    var secondsAway = (int) (calculateArrivalTime - DateTime.Now).TotalSeconds;
                    stringBuilder.Append($"Closest {closest} meters (approx {secondsAway} seconds)");
                    if (closestStation != null)
                    {
                        stringBuilder.Append($" on {closestStation.PlanetInfo.Name}");
                    }
                }

                var bufferedItemCount = PlogPlayerRegistry.LocalPlayer().shippingManager.GetBufferedItemCount(itemId);
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

        public static int FindStationGid(int planetId, int stationId)
        {
            var stationInfo =
                stations.FirstOrDefault(s => s.PlanetInfo.PlanetId == planetId && stationId == s.StationId);
            if (stationInfo != null && stationInfo.StationGid > 0)
            {
                return stationInfo.StationGid;
            }

            foreach (StationComponent stationComponent in GameMain.data.galacticTransport.stationPool)
            {
                if (stationComponent != null && stationComponent.planetId == planetId &&
                    stationComponent.id == stationId)
                {
                    return stationComponent.gid;
                }
            }

            return 0;
        }

        public static StationInfo FindStation(int stationGid, int planetId, int stationId)
        {
            var byStationGid = StationInfo.ByStationGid(stationGid);
            if (byStationGid != null && stationGid != 0)
                return byStationGid;
            var (station, planet) = StationStorageManager.GetStationComp(planetId, stationGid);
            return StationInfo.Build(station, planet).stationInfo;
        }

        public static void CreateOrUpdateStation(StationInfo newStation)
        {
            _stationByGid.TryGetValue(newStation.StationGid, out var existingStation);
            if (existingStation == null)
            {
                _stationByGid.TryAdd(newStation.StationGid, newStation);
                stations.Add(newStation);
                Debug($"Added new station from remote ${newStation.PlanetName}");
            }
            foreach (var product in newStation.Products)
            {
                byItemSummary.TryGetValue(product.ItemId, out var curSummary);
                var newSummary = BuildSummary(newStation, product);
                if (curSummary == null)
                {
                    curSummary = newSummary;
                    byItemSummary.TryAdd(product.ItemId, curSummary);
                }
                else if (existingStation != null)
                {
                    // here we have to adjust the current summary to reflect diff
                    // in amounts between new and old stations
                    var existingPI = existingStation.Products.FirstOrDefault(p => p.ItemId == product.ItemId);
                    var existingSummaryForStation = BuildSummary(existingStation, product);
                    ByItemSummary diff = newSummary - existingSummaryForStation;
                    byItemSummary.TryAdd(product.ItemId, diff + curSummary);
                }
                else
                {
                    // here we just add to the existing summary
                    byItemSummary.TryAdd(product.ItemId, newSummary);
                }
            }
        }

        private static ByItemSummary BuildSummary(StationInfo station, StationProductInfo product)
        {
            return new ByItemSummary
            {
                AvailableItems = product.ItemCount,
                Requesters = station.IsRequested(product.ItemId) ? 1 : 0,
                Suppliers = station.IsSupplied(product.ItemId) ? 1 : 0,
                SuppliedItems = station.IsSupplied(product.ItemId)
                    ? product.ItemCount
                    : 0,
                ProliferatorPoints = product.ProliferatorPoints
            };
        }
    }
}