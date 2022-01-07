using System;
using NebulaAPI;

namespace PersonalLogistics.ModPlayer
{
    public class PlogRemotePlayer : PlogPlayer
    {
        public INebulaPlayer NebulaPlayer;

        public PlogRemotePlayer(PlogPlayerId playerId) : base(playerId, true)
        {
        }

        public override PlogPlayerPosition GetPosition()
        {
            throw new NotImplementedException("GetPosition not impld");
        }

        public override int PackageSize()
        {
            throw new NotImplementedException("PackageSize not impld");
        }

        public override float QueryEnergy(long cost)
        {
            throw new NotImplementedException("QueryEnergy not impld");
        }

        public override void UseEnergy(float energyToUse, int type)
        {
            throw new NotImplementedException("UseEnergy not impld");
        }
    }
}