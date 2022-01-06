using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using PersonalLogistics.Util;

namespace PersonalLogistics.ModPlayer
{
    public static class PlayerStateContainer
    {
        private static readonly ConcurrentDictionary<PlogPlayerId, PlogPlayer> _players = new();

        public static PlogPlayer GetPlayer(PlogPlayerId playerId, bool remote = false)
        {
            lock (_players)
            {
                if (_players.TryGetValue(playerId, out PlogPlayer player))
                {
                    return player;
                }

                if (remote)
                {
                    throw new NotImplementedException($"remote player not yet supported");
                }

                Log.Debug($"Creating new player state for id: {playerId}");
                var newPlayer = PlogPlayerRegistry.RegisterLocal(playerId);
                _players[playerId] = newPlayer;
                return newPlayer;
            }
        }

        public static void Clear()
        {
            lock (_players)
            {
                _players.Clear();
            }
        }

        public static void AddPlayer(PlogPlayer player)
        {
            lock (_players)
            {
                _players[player.playerId] = player;
            }
        }

        public static List<PlogPlayer> GetAllPlayers()
        {
            // clone first
            lock (_players)
            {
                return new List<PlogPlayer>(_players.Values);
            }
        }

        public static void Export(BinaryWriter obj)
        {
            throw new NotImplementedException("Export PSC");
        }

        public static void Import(BinaryReader obj)
        {
            throw new NotImplementedException();
        }
    }
}