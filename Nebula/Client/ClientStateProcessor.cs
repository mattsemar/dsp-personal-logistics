using NebulaAPI;
using PersonalLogistics.ModPlayer;
using PersonalLogistics.Nebula.Packets;
using PersonalLogistics.SerDe;
using PersonalLogistics.Util;

namespace PersonalLogistics.Nebula.Client
{
    [RegisterPacketProcessor]
    public class ClientStateProcessor : BasePacketProcessor<ClientState>
    {
        /// <summary>
        /// Used by clients when the server sends in their state
        /// </summary>
        public override void ProcessPacket(ClientState packet, INebulaConnection conn)
        {
            if (!IsClient)
            {
                Log.Debug($"ignoring client state as host");
                return;
            }
            var (playerId, state) = ClientState.DecodePacket(packet);
            if (playerId != PlogPlayerId.ComputeLocalPlayerId())
            {
                Log.Debug($"ignoring state packet for other player {playerId}");
                return;
            }

            var importRemoteUser = SerDeManager.ImportRemoteUser(playerId, state);
            PlogPlayerRegistry.RegisterLocal(PlogPlayerId.ComputeLocalPlayerId());
            var localPlayer = PlogPlayerRegistry.LocalPlayer();
            Log.Debug($"decoded state from remote: {playerId} {importRemoteUser.inventoryManager.desiredInventoryState.DesiredItems.Count}");
            localPlayer.personalLogisticManager = importRemoteUser.personalLogisticManager;
            localPlayer.shippingManager = importRemoteUser.shippingManager;
            localPlayer.inventoryManager = importRemoteUser.inventoryManager;

            NebulaLoadState.instance.SetClientStateLoaded();
            Log.Info($"Setting local state as loaded {playerId}");
        }
    }
}