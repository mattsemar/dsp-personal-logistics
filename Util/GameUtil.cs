namespace PersonalLogistics.Util
{
    public static class GameUtil
    {
        public static string GetSeed() => GetSeedInt().ToString("D8");

        public static int GetSeedInt() => GameMain.galaxy?.seed ?? 0;

        public static bool HideUiElements()
        {
            var uiGame = UIRoot.instance.uiGame;
            if (uiGame == null)
            {
                return true;
            }

            return uiGame.starmap.active || uiGame.dysonmap.active || uiGame.globemap.active || uiGame.escMenu.active || uiGame.techTree.active;
        }
    }
}