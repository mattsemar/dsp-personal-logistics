﻿using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using PersonalLogistics.Util;

namespace PersonalLogistics.ModPlayer
{
    /// <summary>
    /// This one is for keeping track of the local player, PlayerStateContainer is to be used by the host to maintain
    /// state of clients and to provide a context for their states
    /// </summary>
    public static class PlogPlayerRegistry
    {
        private static readonly ConcurrentDictionary<PlogPlayerId, PlogPlayer> _players = new();
        private static PlogLocalPlayer _localPlayer;
        private static int _localPlayerSeed = -1;
        public static readonly object RegistryLock = new();

        public static PlogPlayer RegisterLocal(PlogPlayerId playerId)
        {
            lock (RegistryLock)
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
        }

        public static List<PlogPlayer> GetAllPlayers()
        {
            return _players.Values.ToList();
        }

        public static PlogLocalPlayer LocalPlayer()
        {
            lock (RegistryLock)
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
        }

        public static PlogPlayer Get(PlogPlayerId playerId)
        {
            lock (RegistryLock)
            {
                if (!_players.ContainsKey(playerId))
                {
                    Log.Debug($"playerid: {playerId} not found in list. local is {_localPlayer}");
                }

                return _players[playerId];
            }
        }

        public static void ClearLocal()
        {
            lock (RegistryLock)
            {
                Log.Debug("clearing local player");
                if (_localPlayer == null)
                {
                    return;
                }

                _players.TryRemove(_localPlayer.playerId, out var _);
                _localPlayer = null;
            }
        }

#if DEBUG
        public static void RestorePretestLocalPlayer(PlogLocalPlayer preTestPlayer)
        {
            lock (RegistryLock)
            {
                if (_players.ContainsKey(preTestPlayer.playerId))
                { 
                    // not sure about this
                    
                }

                _players[preTestPlayer.playerId] = preTestPlayer;
                _localPlayer = preTestPlayer;
            }
        }
#endif

        public static bool IsLocalPlayerPlanet(int planetId)
        {
            lock (RegistryLock)
            {
                if (_localPlayer == null)
                    return false;
                return (_localPlayer.PlanetId() == planetId);
            }
        }
    }
}