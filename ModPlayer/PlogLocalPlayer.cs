using System;
using PersonalLogistics.Scripts;
using PersonalLogistics.Util;
using UnityEngine;

namespace PersonalLogistics.ModPlayer
{
    public class PlogLocalPlayer : PlogPlayer
    {
        public PlogLocalPlayer(PlogPlayerId playerId, Player player) : base(playerId, false, player)
        {
            recycleWindowPersistence = new RecycleWindowPersistence(playerId);
        }

        public override PlogPlayerPosition GetPosition()
        {
            try
            {
                return new PlogPlayerPosition
                {
                    clusterPosition = GameMain.mainPlayer.uPosition,
                    planetPosition = GameMain.mainPlayer.position
                };
            }
            catch (Exception e)
            {
                Log.Warn($"failed to get player position {e.Message} {e.StackTrace}");
                return
                    new PlogPlayerPosition
                    {
                        clusterPosition = VectorLF3.one,
                        planetPosition = Vector3.one
                    };
            }
        }

        public override int PackageSize()
        {
            return GameMain.mainPlayer.package.size;
        }

        public override int PlanetId()
        {
            return GameMain.mainPlayer.planetId;
        }

        public override float QueryEnergy(long cost)
        {
            GameMain.mainPlayer.mecha.QueryEnergy(cost, out var _, out var ratio);
            return ratio;
        }

        public override void UseEnergy(float energyToUse, int type)
        {
            GameMain.mainPlayer.mecha.MarkEnergyChange(Mecha.EC_DRONE, -energyToUse);
            GameMain.mainPlayer.mecha.UseEnergy(energyToUse);
        }

        public override void NotifyLeavePlanet()
        {
            Log.Debug("Player is departing planet");
            if (PluginConfig.stationRequestMode.Value != StationSourceMode.Planetary)
            {
                return;
            }

            if (PluginConfig.planetarySourceMode.Value != PlanetarySourceMode.ReturnBufferOnDepart)
            {
                return;
            }

            personalLogisticManager.CancelInboundRequests();
            var remainingItems = shippingManager.MoveAllBufferedItemsToLogisticsSystem(true);
            if (remainingItems > 0)
                Log.LogAndPopupMessage($"{remainingItems} unable to be returned to Logistics Stations");
            else
                Log.LogAndPopupMessage($"Returned all buffered items to Logistics Stations");
        }
    }
}