using BepInEx.Logging;

namespace PersonalLogistics
{
    public class Log
    {
        public static ManualLogSource logger;

        public static void Debug(string message)
        {
            logger.LogDebug(message);
        }
        public static void Info(string message)
        {
            logger.LogInfo(message);
        }

        public static void Warn(string message)
        {
            logger.LogWarning(message);
        }

        public static void LogAndPopupMessage(string message)
        {
            UIRealtimeTip.Popup(message);
            logger.LogWarning($"Popped up message {message}");
        }
    }
}