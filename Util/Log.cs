using System;
using System.Collections.Generic;
using BepInEx.Logging;

namespace PersonalLogistics.Util
{
    public static class Log
    {
        public static ManualLogSource logger;

        public static void Debug(string message)
        {
            logger.LogDebug($"[{DateTime.Now:HH:mm:ss.fff}] {message}");
        }

        public static void Info(string message)
        {
            logger.LogInfo($"[{DateTime.Now:HH:mm:ss.fff}] {message}");
        }

        public static void Warn(string message)
        {
            logger.LogWarning($"[{DateTime.Now:HH:mm:ss.fff}] {message}");
        }

        public static void LogAndPopupMessage(string message)
        {
            UIRealtimeTip.Popup(message);
            logger.LogWarning($"Popped up message {message}");
        }


        private static Dictionary<string, DateTime> _lastPopupTime = new Dictionary<string, DateTime>();

        public static void LogPopupWithFrequency(string msgTemplate, params object[] args)
        {
            if (!_lastPopupTime.TryGetValue(msgTemplate, out DateTime lastTime))
                lastTime = DateTime.Now.Subtract(TimeSpan.FromSeconds(500));
            try
            {
                var msg = string.Format(msgTemplate, args);
                if ((DateTime.Now - lastTime).TotalMinutes < 2)
                {
                    Debug($"(Popup suppressed) {msg}");
                    return;
                }

                _lastPopupTime[msgTemplate] = DateTime.Now;
                LogAndPopupMessage(msg);
            }
            catch (Exception e)
            {
                Warn($"exception with popup: {e.Message}\r\n {e}\r\n{e.StackTrace}\r\n{msgTemplate}");
            }
        }
    }
}