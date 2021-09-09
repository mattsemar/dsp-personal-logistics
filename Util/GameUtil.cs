namespace PersonalLogistics.Util
{
    public static class GameUtil   
    {
        public static string GetSeed()
        {
            return GetSeedInt().ToString("D8");
        }
        
        public static int GetSeedInt()
        {
            return GameMain.galaxy?.seed ?? 0;
        }
    }
}