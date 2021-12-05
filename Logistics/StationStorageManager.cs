using System;
using JetBrains.Annotations;
using UnityEngine;
using static PersonalLogistics.Util.Log;

namespace PersonalLogistics.Logistics
{
    public static class StationStorageManager
    {
        public static int RemoveFromStation(StationInfo stationInfo, int itemId, int count)
        {
            var stationComponent = GetStationComp(stationInfo);
            if (stationComponent == null)
            {
                Warn($"unable to remove items from station {itemId}");
                return 0;
            }

            var countToTake = count;
            var itemIdToTake = itemId;
            stationComponent.TakeItem(ref itemIdToTake, ref countToTake);
            return countToTake;
        }

        public static bool RemoveWarperFromStation(StationInfo stationInfo)
        {
            var removed = RemoveFromStation(stationInfo, Mecha.WARPER_ITEMID, 1);
            if (removed > 0)
            {
                return true;
            }

            var stationComponent = GetStationComp(stationInfo);
            if (stationComponent == null)
            {
                Warn($"unable to remove warper from {stationInfo.stationId} on {stationInfo.PlanetName}");
                return false;
            }

            if (stationComponent.warperCount > 1)
            {
                stationComponent.warperCount--;
                Debug($"Consumed warper from station {stationInfo.stationId} on {stationInfo.PlanetName}");
                return true;
            }

            Debug("No warpers available on station, will not deduct");

            return false;
        }

        public static (long energyCost, bool warperNeeded) CalculateTripEnergyCost(StationInfo stationInfo, double distance, float sailSpeed)
        {
            var stationComponent = GetStationComp(stationInfo);
            if (stationComponent == null)
            {
                Warn($"unable to calculate energy cost for station id {stationInfo.stationId} on {stationInfo.PlanetName}");
                return (-1, false);
            }

            var energyCost = stationComponent.CalcTripEnergyCost(distance, sailSpeed, true);
            return (energyCost, stationComponent.warpEnableDist < distance);
        }

        [CanBeNull]
        private static StationComponent GetStationComp(StationInfo stationInfo)
        {
            try
            {
                var planetById = GameMain.galaxy.PlanetById(stationInfo.PlanetInfo.PlanetId);
                var stationComponent = planetById.factory.transport.stationPool[stationInfo.stationId];
                return stationComponent;
            }
            catch (Exception e)
            {
                logger.LogWarning($"failed to get station comp {e.Message}");
                logger.LogWarning(e.StackTrace);
            }

            return null;
        }

        public static int AddToStation(StationInfo stationInfo, int itemId, int amountToAdd)
        {
            var stationComponent = GetStationComp(stationInfo);
            if (stationComponent == null)
            {
                Warn($"unable to add items to station {itemId} {stationInfo.PlanetName} {stationInfo.stationId}");
                return 0;
            }

            var countToAdd = Math.Min(stationInfo.StationType == StationType.ILS ? 10_000 : 5_000, amountToAdd);
            var itemIdToTake = itemId;

            return stationComponent.AddItem(itemIdToTake, countToAdd);
        }

        public static long RemoveEnergyFromStation(StationInfo stationInfo, long energy)
        {
            var stationComponent = GetStationComp(stationInfo);
            if (stationComponent == null)
            {
                Warn($"unable to remove energy from station on {stationInfo.PlanetName} {stationInfo.stationId} {stationInfo.PlanetInfo.lastLocation}");
                return 0;
            }

            var energyRemoved = Math.Min(energy, stationComponent.energy);
            stationComponent.energy -= energyRemoved;
            return energyRemoved;
        }

        public static double GetDistance(VectorLF3 playerUPosition, Vector3 playerLocalPosition, StationInfo stationInfo)
        {
            var uDistance = playerUPosition.Distance(stationInfo.PlanetInfo.lastLocation);
            if (uDistance < 600)
            {
                return Vector3.Distance(playerLocalPosition, stationInfo.localPosition);
            }

            return uDistance;
        }
    }
}