namespace PersonalLogistics.ModPlayer
{
    public class PLOGLocalPlayer : PlogPlayer
    {
        public PLOGLocalPlayer(PlogPlayerId playerId, Player player = null) : base(playerId, false, player)
        {
        }

        public override PlogPlayerPosition GetPosition()
        {
            return new PlogPlayerPosition
            {
                clusterPosition = _localPlayer.uPosition,
                planetPosition = _localPlayer.position
            };
        }

        public override int PackageSize()
        {
            return _localPlayer.package.size;
        }

        public override float QueryEnergy(long cost)
        {
            _localPlayer.mecha.QueryEnergy(cost, out var _, out var ratio);
            return ratio;
        }

        public override void UseEnergy(float energyToUse, int type)
        {
            _localPlayer.mecha.MarkEnergyChange(Mecha.EC_DRONE, -energyToUse);
            _localPlayer.mecha.UseEnergy(energyToUse);
        }
    }
}