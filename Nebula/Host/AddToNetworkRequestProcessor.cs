using NebulaAPI;
using PersonalLogistics.Logistics;
using PersonalLogistics.Model;
using PersonalLogistics.Nebula.Packets;
using PersonalLogistics.Util;

namespace PersonalLogistics.Nebula.Host
{
    [RegisterPacketProcessor]
    public class AddToNetworkRequestProcessor : BasePacketProcessor<AddToNetworkRequest>
    {
        public override void ProcessPacket(AddToNetworkRequest packet, INebulaConnection conn)
        {
            if (IsClient)
                return;

            var remainingItemStack = LogisticsNetwork.AddItem(packet.playerUPosition.ToVectorLF3(), packet.itemId, ItemStack.FromCountAndPoints(packet.itemCount, packet.proliferatorPoints));
            Log.Info($"Added items to network on behalf of player {packet.clientId} {packet.itemId}. Added count {remainingItemStack.ItemCount}/{packet.itemCount}");
            if (remainingItemStack.ItemCount == 0)
            {
                // don't be too chatty, just let them assume (correctly) that he item was added
                return;
            }
            INetworkProvider network = NebulaModAPI.MultiplayerSession.Network;
            var nebulaPlayer = network.PlayerManager.GetPlayer(conn);
            
            nebulaPlayer.SendPacket(new AddToNetworkResponse(
                packet.clientId,
                packet.itemId, 
                remainingItemStack));
        }
    }
}