using System;
using CommonAPI.Systems;
using JetBrains.Annotations;
using PersonalLogistics.Model;
using PersonalLogistics.Nebula;
using PersonalLogistics.Nebula.Client;
using PersonalLogistics.Shipping;
using UnityEngine;
using static PersonalLogistics.Util.Log;

namespace PersonalLogistics.Logistics
{
    public static class StationStorageManager
    {
        public static ItemStack RemoveFromStation(StationInfo stationInfo, int itemId, int count)
        {
            var stationComponent = GetStationComp(stationInfo);
            if (stationComponent == null)
            {
                Warn($"unable to remove items from station {itemId}");
                return ItemStack.Empty();
            }

            try
            {
                var countToTake = count;
                var itemIdToTake = itemId;
                stationComponent.TakeItem(ref itemIdToTake, ref countToTake, out int inc);
                return ItemStack.FromCountAndPoints(countToTake, inc);
            }
            catch (Exception e)
            {
                // this can happen if the station is being torn down while we're trying to remove from it
                Warn($"Got an exception removing items from stationComponent. assuming this is fine: {e.Message}");
                return ItemStack.Empty();
            }
        }

        public static bool RemoveWarperFromStation(StationInfo stationInfo)
        {
            var removed = RemoveFromStation(stationInfo, Mecha.WARPER_ITEMID, 1);
            if (removed.ItemCount > 0)
            {
                return true;
            }

            try
            {
                var stationComponent = GetStationComp(stationInfo);
                if (stationComponent == null)
                {
                    Warn($"unable to remove warper from {stationInfo.StationId} on {stationInfo.PlanetName}");
                    return false;
                }

                if (stationComponent.warperCount > 1)
                {
                    stationComponent.warperCount--;
                    Debug($"Consumed warper from station {stationInfo.StationId} on {stationInfo.PlanetName}");
                    return true;
                }

                Debug("No warpers available on station, will not deduct");

                return false;
            }
            catch (Exception e)
            {
                Warn($"Got exception removing warper from station {e.Message}");
                return false;
            }
        }

        public static (long energyCost, bool warperNeeded) CalculateTripEnergyCost(StationInfo stationInfo, double distance)
        {
            var stationComponent = GetStationComp(stationInfo);
            if (stationComponent == null)
            {
                Warn($"unable to calculate energy cost for station id {stationInfo?.StationId} on {stationInfo?.PlanetName}");
                return (-1, false);
            }

            var useWarper = ShippingCostCalculator.UseWarper(distance, stationInfo);
            long energyCost;
            if (distance > 5_000)
            {
                energyCost = ShippingCostCalculator.CalcRemoteTripEnergyCost(distance, stationInfo);
            }
            else
            {
                energyCost = ShippingCostCalculator.CalcLocalTripEnergyCost(GameMain.mainPlayer.position, stationInfo.LocalPosition);
            }

            return (energyCost, useWarper);
        }

        [CanBeNull]
        public static StationComponent GetStationComp(StationInfo stationInfo)
        {
            try
            {
                var planetById = GameMain.galaxy.PlanetById(stationInfo.PlanetInfo.PlanetId);
                var stationComponent = planetById.factory.transport.stationPool[stationInfo.StationId];
                return stationComponent;
            }
            catch (Exception e)
            {
                logger.LogWarning($"failed to get station comp {e.Message}");
                logger.LogWarning(e.StackTrace);
            }

            return null;
        }

        public static int AddToStation(StationInfo stationInfo, int itemId, ItemStack amount)
        {
            var stationComponent = GetStationComp(stationInfo);
            if (stationComponent == null)
            {
                Warn($"unable to add items to station {itemId} {stationInfo.PlanetName} {stationInfo.StationId}");
                return 0;
            }

            var countToAdd = Math.Min(stationInfo.StationType == StationType.ILS ? 10_000 : 5_000, amount.ItemCount);
            var itemIdToTake = itemId;
            var toAdd = amount.Remove(countToAdd);
            Debug($"calling additem on station comp with {toAdd.ItemCount}, {toAdd.ProliferatorPoints}, amount now: {amount.ItemCount} {amount}");
            return stationComponent.AddItem(itemIdToTake, toAdd.ItemCount, toAdd.ProliferatorPoints);
        }

        public static long RemoveEnergyFromStation(StationInfo stationInfo, long energy)
        {
            var stationComponent = GetStationComp(stationInfo);
            if (stationComponent == null)
            {
                Warn($"unable to remove energy from station on {stationInfo.PlanetName} {stationInfo.StationId} {stationInfo.PlanetInfo.lastLocation}");
                return 0;
            }

            var energyRemoved = Math.Min(energy, stationComponent.energy);
            stationComponent.energy -= energyRemoved;
            return energyRemoved;
        }

        public static double GetDistance(VectorLF3 playerUPosition, Vector3 playerLocalPosition, StationInfo stationInfo)
        {
            var uDistance = playerUPosition.Distance(stationInfo.PlanetInfo.lastLocation);
            if (uDistance < 1600)
            {
                return Vector3.Distance(playerLocalPosition, stationInfo.LocalPosition);
            }

            return uDistance;
        }

        public static (StationComponent station, PlanetData planet) GetStationComp(int planetId, int stationId)
        {
            try
            {
                var planetById = GameMain.galaxy.PlanetById(planetId);
                if (planetById == null)
                {
                    var stationComp = GetStationComp(0, false, planetId, stationId);
                    return (stationComp, null);
                }

                var stationComponent = planetById.factory.transport.stationPool[stationId];
                return (stationComponent, planetById);
            }
            catch (Exception e)
            {
                logger.LogWarning($"failed to get station comp {e.Message}");
                logger.LogWarning(e.StackTrace);
            }

            return (null, null);
        }

        public static StationComponent GetStationComp(int stationGid, bool useGid = true, int stationId = 0, int planetId = 0)
        {
            foreach (PlanetFactory fc in GameMain.data.factories)
            {
                foreach (StationComponent sc in fc.transport.stationPool)
                {
                    if (sc != null && useGid && sc.gid == stationGid)
                    {
                        return sc;
                    }

                    if (sc != null && !useGid && sc.planetId == planetId && stationId == sc.id)
                    {
                        return sc;
                    }
                }
            }

            return null;
        }
    }
}