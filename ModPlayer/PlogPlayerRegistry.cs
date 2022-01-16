using System.Collections.Generic;
using System.IO;
using System.Linq;
using PersonalLogistics.Util;
using UnityEngine;

namespace PersonalLogistics.ModPlayer
{
    /// <summary>
    /// This one is for keeping track of the local player, PlayerStateContainer is to be used by the host to maintain
    /// state of clients and to provide a context for their states
    /// </summary>
    public static class PlogPlayerRegistry
    {
        private static readonly Dictionary<PlogPlayerId, PlogPlayer> _players = new();
        private static PlogLocalPlayer _localPlayer;
        private static int _localPlayerSeed = -1;

        public static PlogPlayer RegisterLocal(PlogPlayerId playerId)
        {
            var plogPlayer = new PlogLocalPlayer(playerId, GameMain.mainPlayer);

            if (_players.ContainsKey(playerId) && _localPlayer.playerId == playerId)
            {
                Log.Debug($"returning existing previous local player {playerId}");
                return _localPlayer;
            }

            _localPlayer = plogPlayer;
            PlayerStateContainer.AddPlayer(_localPlayer);
            _players[playerId] = plogPlayer;
            return plogPlayer;
        }

        public static List<PlogPlayer> GetAllPlayers()
        {
            return _players.Values.ToList();
        }

        public static PlogLocalPlayer LocalPlayer()
        {
            if (_localPlayer == null)
            {
                Log.Debug($"local player not assigned. {_players.Count}");
                return null;
            }

            if (_localPlayerSeed != GameUtil.GetSeedInt() && !DSPGame.IsMenuDemo && GameUtil.GetSeedInt() != 0)
            {
                var computedLocalPlayerId = PlogPlayerId.ComputeLocalPlayerId();
                if (_localPlayer.playerId != computedLocalPlayerId)
                {
                    Log.Debug($"local player is mismatched {_localPlayer.playerId} vs {computedLocalPlayerId}");
                }

                _localPlayerSeed = computedLocalPlayerId.gameSeed;
            }

            return _localPlayer;
        }

        public static PlogPlayer Get(PlogPlayerId playerId)
        {
            if (!_players.ContainsKey(playerId))
            {
                Log.Debug($"playerid: {playerId} not found in list. local is {_localPlayer}");
            }
            return _players[playerId];
        }

        public static void ClearLocal()
        {
            Log.Debug($"clearing local player: ${StackTraceUtility.ExtractStackTrace()}");
            if (_localPlayer == null)
            {
                return;
            }

            _players.Remove(_localPlayer.playerId);
            _localPlayer = null;
        }

        public static bool IsLocalPlayerPlanet(int planetId)
        {
            if (_localPlayer == null)
                return false;
            return (_localPlayer.PlanetId() == planetId);
        }
    }
}