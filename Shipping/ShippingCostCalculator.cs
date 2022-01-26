using System;
using PersonalLogistics.Logistics;
using PersonalLogistics.Util;
using UnityEngine;

namespace PersonalLogistics.Shipping
{
    public static class ShippingCostCalculator
    {
        private const long shipEnergyPerMaxSpeed = 200_000;
        private const long shipEnergyPerMeter = 30;

        public static long CalcRemoteTripEnergyCost(double distance, StationInfo stationInfo)
        {
            var useWarper = UseWarper(distance, stationInfo);
            double adjustedDistance = distance * 0.03 + 100.0;
            if (adjustedDistance > 3_000.0)
                adjustedDistance = 3_000.0;
            double energyCost = adjustedDistance * shipEnergyPerMaxSpeed;
            if (useWarper)
                energyCost += 100_000_000.0;
            return (long)(6_000_000.0 + distance * shipEnergyPerMeter + energyCost);
        }

        public static long CalcLocalTripEnergyCost(Vector3 source, Vector3 dest)
        {
            double sourceMagnitude = Mathf.Sqrt(source.x * source.x + source.y * source.y + source.z * source.z);
            double destMagnitude = Mathf.Sqrt(dest.x * dest.x + dest.y * dest.y + dest.z * dest.z);
            double halfDistance = (sourceMagnitude + sourceMagnitude) * 0.5;
            
            double d = (source.x * dest.x + source.y * dest.y + source.z * dest.z) / (sourceMagnitude * destMagnitude);
            double angle = Math.Acos(d);
            double num20 = halfDistance * angle;
            return  (long)(num20 * 20_000.0 * 2.0 + 800_000.0);
        }

        private static DateTime AddSeconds(double seconds)
        {
#if DEBUG
            var newSeconds = PluginConfig.overriddenTransitTimeSeconds.Value > 0.0001 ? PluginConfig.overriddenTransitTimeSeconds.Value : seconds;
            Log.Debug($"some cheating is happening here from {seconds} to {newSeconds}");
            seconds = newSeconds;
#endif            
            return DateTime.Now.AddSeconds(seconds);
        }
        
        public static DateTime CalculateArrivalTime(double oneWayDistance, StationInfo stationInfo)
        {
            var distance = oneWayDistance * 2;
            var droneSpeed = GameMain.history.logisticDroneSpeedModified;
            
            if (UseWarper(oneWayDistance, stationInfo))
            {
                // must use warpers here
                // t = d/r
                var betweenPlanetsTransitTime = distance / GameMain.history.logisticShipWarpSpeedModified;
                // transit time between planets plus a little extra to get to an actual spot on the planet
                return AddSeconds(betweenPlanetsTransitTime + 600 / droneSpeed);
            }

            if (distance > 5000)
            {
                var betweenPlanetsTransitTime = distance / GameMain.history.logisticShipSailSpeedModified;
                // transit time between planets plus a little extra to get to an actual spot on the planet
                return AddSeconds(betweenPlanetsTransitTime + 600 / droneSpeed);
            }

            // less than 5 km, we consider that to be on the same planet as us
            return AddSeconds(distance / droneSpeed);
        }

        public static bool UseWarper(double oneWayDistance, StationInfo stationInfo)
        {
            var distance = oneWayDistance * 2;

            if (distance < 5000 || !GameMain.history.logisticShipWarpDrive)
                return false;
            return PluginConfig.GetMinWarpDistanceMeters(stationInfo.WarpEnableDistance) < oneWayDistance;
        }
    }
}