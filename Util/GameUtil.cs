namespace PersonalLogistics.Util
{
    public static class GameUtil
    {
        public static string GetSeed() => GetSeedInt().ToString("D8");

        public static int GetSeedInt() => GameMain.galaxy?.seed ?? 0;
    }
}