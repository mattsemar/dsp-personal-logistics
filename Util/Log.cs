using System;
using System.Collections.Generic;
using BepInEx.Logging;

namespace PersonalLogistics.Util
{
    public static class Log
    {
        public static ManualLogSource logger;


        private static readonly Dictionary<string, DateTime> _lastPopupTime = new();

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

        public static void LogAndPopupMessage(string message, bool beep = false)
        {
            UIRealtimeTip.PopupAhead(message, beep);
            logger.LogWarning($"Popped up message {message}");
        }

        public static void LogPopupWithFrequency(string msgTemplate, params object[] args)
        {
            if (!_lastPopupTime.TryGetValue(msgTemplate, out var lastTime))
            {
                lastTime = DateTime.Now.Subtract(TimeSpan.FromSeconds(500));
            }

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

        public static void Trace(string msg)
        {
#if DEBUG
            logger.LogInfo($"[{DateTime.Now:HH:mm:ss.fff}] {msg}");
#endif
        }
    }
}