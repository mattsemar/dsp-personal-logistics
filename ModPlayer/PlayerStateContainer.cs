using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PersonalLogistics.SerDe;
using PersonalLogistics.Util;
using UnityEngine;

namespace PersonalLogistics.ModPlayer
{
    public static class PlayerStateContainer
    {
        private static readonly Dictionary<PlogPlayerId, PlogPlayer> _players = new();

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
                    var remotePlayer = new PlogRemotePlayer(playerId);
                    Log.Debug($"created new remote player for id: {playerId}");
                    _players[playerId] = remotePlayer;
                    return remotePlayer;
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
    }

    public class PlayerStateContainerPersistence : InstanceSerializer
    {
        private readonly PlogPlayerId _playerId;
        private Dictionary<PlogPlayerId, PlogPlayer> _importedPlayers;


        public PlayerStateContainerPersistence(PlogPlayerId playerId)
        {
            _playerId = playerId;
        }

        public override void ExportData(BinaryWriter w)
        {
            var plogPlayers = PlayerStateContainer.GetAllPlayers();
            var remotePlayers = plogPlayers.FindAll(p => p is PlogRemotePlayer)
                .Select(p => p as PlogRemotePlayer).ToList();
            Log.Debug($"Writing out {remotePlayers.Count} of {plogPlayers.Count} total players");
            w.Write(remotePlayers.Count);

            foreach (var plogPlayer in remotePlayers)
            {
                PlogPlayerId.Export(plogPlayer.playerId, w);
                var playerBytes = SerDeManager.ExportRemoteUserData(plogPlayer);
                w.Write(playerBytes.Length);
                w.Write(playerBytes);
            }
        }

        public override void ImportData(BinaryReader reader)
        {
            var playerCount = reader.ReadInt32();
            Debug.Log($"reading {playerCount} remote players");
            _importedPlayers = new Dictionary<PlogPlayerId, PlogPlayer>();
            for (int i = 0; i < playerCount; i++)
            {
                var playerId = PlogPlayerId.Import(reader);
                var playerDataLength = reader.ReadInt32();
                Log.Debug($"reading {playerDataLength} bytes for player {playerId}");
                var playerBytes = reader.ReadBytes(playerDataLength);
                try
                {
                    var importedUser = SerDeManager.ImportRemoteUser(playerId, playerBytes);
                    _importedPlayers.Add(playerId, importedUser);
                    Log.Debug($"Imported remote user: {playerId} {importedUser.SummarizeState()}");
                }
                catch (Exception e)
                {
                    Log.Warn($"failed to import user {playerId} from {playerDataLength} bytes\n{e}{e.StackTrace}");
                }
                Log.Debug($"clearing psc and adding {_importedPlayers.Count} new users");
                PlayerStateContainer.Clear();
                foreach (var importedPlayerId in _importedPlayers.Keys)
                {
                    PlayerStateContainer.AddPlayer(_importedPlayers[importedPlayerId]);
                }               
            }
        }

        public override PlogPlayerId GetPlayerId() => _playerId;

        public override string GetExportSectionId() => "PSC";

        public override void InitOnLoad()
        {
        }
        
        public override string SummarizeState() => $"PSC: {_importedPlayers.Count} remote players";
    }
}