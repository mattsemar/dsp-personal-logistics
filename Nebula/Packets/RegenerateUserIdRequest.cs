using System;
using System.IO;
using PersonalLogistics.ModPlayer;

namespace PersonalLogistics.Nebula.Packets
{
    public class RegenerateUserIdRequest
    {
        public string playerId { get; set; }

        public RegenerateUserIdRequest()
        {
        }

        public RegenerateUserIdRequest(PlogPlayerId playerId)
        {
            this.playerId = playerId.ToString();
        }
    }
}