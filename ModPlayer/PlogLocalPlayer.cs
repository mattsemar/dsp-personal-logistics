using PersonalLogistics.Scripts;

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
            return new PlogPlayerPosition
            {
                clusterPosition = GameMain.mainPlayer.uPosition,
                planetPosition = GameMain.mainPlayer.position
            };
        }

        public override int PackageSize()
        {
            return GameMain.mainPlayer.package.size;
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
    }
}