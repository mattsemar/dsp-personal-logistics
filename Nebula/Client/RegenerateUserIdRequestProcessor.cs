using NebulaAPI;
using PersonalLogistics.ModPlayer;
using PersonalLogistics.Nebula.Packets;
using PersonalLogistics.SerDe;
using PersonalLogistics.Util;

namespace PersonalLogistics.Nebula.Client
{
    [RegisterPacketProcessor]
    public class RegenerateUserIdRequestProcessor : BasePacketProcessor<RegenerateUserIdRequest>
    {
        /// <summary>
        /// Used by clients when the server tells them to regenerate user id
        /// </summary>
        public override void ProcessPacket(RegenerateUserIdRequest packet, INebulaConnection conn)
        {
            if (!IsClient)
            {
                Log.Debug("Ignoring regenerate request as host");
                return;
            }
            
            if (PlogPlayerId.FromString(packet.playerId) != PlogPlayerId.ComputeLocalPlayerId())
            {
                Log.Debug($"ignoring regenerate packet for other player {packet.playerId}");
                return;
            }

            var newUserId = PluginConfig.RegenerateAssignedUserId();
            Log.Debug($"Assigned new id to client: ${newUserId}");
            NebulaLoadState.instance = new NebulaLoadState();
        }
    }
}