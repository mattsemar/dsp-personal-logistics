using System.Collections.Generic;
using System.IO;
using System.Linq;
using PersonalLogistics.Util;
using UnityEngine;

namespace PersonalLogistics.ModPlayer
{
    public static class PlogPlayerRegistry
    {
        private static readonly Dictionary<PlogPlayerId, PlogPlayer> _players = new();
        private static PlogPlayer _localPlayer;

        public static PlogPlayer RegisterLocal(PlogPlayerId playerId)
        {
            var plogPlayer = new PLOGLocalPlayer(playerId, GameMain.mainPlayer);

            if (_players.ContainsKey(playerId))
            {
                var existingPlayer = _players[playerId];
                throw new InvalidDataException($"tried to re-register same player {playerId} previous: {existingPlayer}, new: {plogPlayer}");
            }
            _localPlayer = plogPlayer;
            return _players[playerId] = plogPlayer;
        }

        public static List<PlogPlayer> GetAllPlayers()
        {
            return _players.Values.ToList();
        }

        public static PlogPlayer LocalPlayer()
        {
            if (_localPlayer == null)
            {
                Log.Debug($"local player not assigned. {_players.Count}");
                return null;
            }

            var computedLocalPlayerId = PlogPlayerId.ComputeLocalPlayerId();
            if (_localPlayer.playerId != computedLocalPlayerId)
            {
                throw new InvalidDataException($"local player is mismatched {_localPlayer.playerId} vs {computedLocalPlayerId}");
            }

            return _localPlayer;
        }

        public static PlogPlayer Get(PlogPlayerId playerId)
        {
            return _players[playerId];
        }

        public static void ClearLocal()
        {
            if (_localPlayer == null)
            {
                return;
            }

            _players.Remove(_localPlayer.playerId);
            _localPlayer = null;
        }
    }
}