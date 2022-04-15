using System.Reflection;
using NebulaAPI;
using PersonalLogistics.ModPlayer;
using PersonalLogistics.Nebula.Client;
using PersonalLogistics.Util;

namespace PersonalLogistics.Nebula
{
    public class NebulaLoadState
    {
        public static NebulaLoadState instance;
        private static bool _isRegistered;
        private bool _clientStateLoadedFromServer;
        private bool _clientStateRequested;

        public static bool IsMultiplayerClient()
        {
            if (!NebulaModAPI.NebulaIsInstalled || NebulaModAPI.MultiplayerSession == null || NebulaModAPI.MultiplayerSession.LocalPlayer == null || !NebulaModAPI.IsMultiplayerActive)
            {
                return false;
            }

            return NebulaModAPI.MultiplayerSession.LocalPlayer.IsClient;
        }
        public static bool IsMultiplayerHost()
        {
            if (!NebulaModAPI.NebulaIsInstalled || NebulaModAPI.MultiplayerSession == null || NebulaModAPI.MultiplayerSession.LocalPlayer == null || !NebulaModAPI.IsMultiplayerActive)
            {
                return false;
            }

            return NebulaModAPI.MultiplayerSession.LocalPlayer.IsHost;
        }


        public static void Register()
        {
            if (_isRegistered)
                return;

            NebulaModAPI.RegisterPackets(Assembly.GetExecutingAssembly());
            _isRegistered = true;
        }

        public static void Reset()
        {
            instance._clientStateRequested = false;
            instance._clientStateLoadedFromServer = false;
            instance = null;
        }

        public bool IsWaitingClient()
        {
            if (!IsMultiplayerClient())
                return false;
            return !_clientStateLoadedFromServer;
        }

        public void SetClientStateLoaded()
        {
            _clientStateLoadedFromServer = true;
        }


        public void RequestStateFromHost()
        {
            if (_clientStateRequested || !IsMultiplayerClient())
                return;
            if (!NebulaModAPI.MultiplayerSession.IsGameLoaded)
                return;

            Log.Debug($"Requesting state from host {PlogPlayerId.ComputeLocalPlayerId()}");
            RequestClient.RequestStateFromHost();
            _clientStateRequested = true;
        }
    }
}