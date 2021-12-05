using System;
using System.Collections.Generic;
using System.Text;
using PersonalLogistics.Util;
using UnityEngine;
using static PersonalLogistics.Util.Log;

namespace PersonalLogistics.Model
{
    public class CrossSeedInventoryState
    {
        public static bool IsInitialized;
        private readonly Dictionary<string, DesiredInventoryState> _states = new Dictionary<string, DesiredInventoryState>();

        public static CrossSeedInventoryState instance { get; private set; }

        public static void Init()
        {
            if (IsInitialized)
            {
                return;
            }

            if (instance != null)
            {
                IsInitialized = true;
                return;
            }

            if (GameMain.galaxy == null || GameMain.galaxy.seed == 0)
            {
                return;
            }

            if (!PluginConfig.Initted())
            {
                Debug("Still waiting to init cross seed state");
                return;
            }

            var strVal = PluginConfig.crossSeedInvState.Value;
            // format is "seedStr__JSONREP$seedStr__JSONREP"
            var parts = strVal.Split('$');
            // var states = new List<DesiredInventoryState>();
            var newInstance = new CrossSeedInventoryState();
            if (parts.Length < 1)
            {
                instance = newInstance;
                IsInitialized = true;
            }

            foreach (var savedValueForSeedStr in parts)
            {
                var firstUnderscoreIndex = savedValueForSeedStr.IndexOf('_');
                if (firstUnderscoreIndex == -1)
                {
                    Warn($"failed to convert parts into seed and JSON {savedValueForSeedStr}");
                    continue;
                }

                var seedString = savedValueForSeedStr.Substring(0, firstUnderscoreIndex);
                var jsonStrWithLeadingUnderscore = savedValueForSeedStr.Substring(firstUnderscoreIndex + 1);
                if (seedString.Length < 3 || jsonStrWithLeadingUnderscore[0] != '_')
                {
                    Warn($"invalid parsing of parts {seedString} {jsonStrWithLeadingUnderscore}");
                    continue;
                }

                try
                {
                    var desiredInventoryState = DesiredInventoryState.LoadStored(jsonStrWithLeadingUnderscore.Substring(1));
                    newInstance._states[seedString.Trim()] = desiredInventoryState;
                    Debug($"Added saved state for seed {seedString}. Total {newInstance._states.Count}");
                }
                catch (Exception e)
                {
                    Warn($"Failed to deserialize stored inventory state {e}\r\n{e.StackTrace}");
                }
            }


            instance = newInstance;
            IsInitialized = true;
        }

        public DesiredInventoryState GetStateForSeed(string seed)
        {
            if (!_states.ContainsKey(seed))
            {
                _states[seed] = new DesiredInventoryState();
            }

            return _states[seed];
        }

        public void SetStateForSeed(string seed, DesiredInventoryState state)
        {
            _states[seed] = state;
        }

        public static List<(string seed, string stateString)> GetStatesForOtherSeeds(string curSeed)
        {
            if (instance == null)
            {
                Warn("GetStatesForOtherSeeds called before init");
                return new List<(string seed, string stateString)>();
            }

            return instance.GetStatesForOtherSeedsImpl(curSeed);
        }

        public List<(string seed, string stateString)> GetStatesForOtherSeedsImpl(string curSeed)
        {
            var result = new List<(string seed, string stateString)>();
            foreach (var seedStr in _states.Keys)
            {
                if (curSeed == seedStr)
                {
                    continue;
                }

                result.Add((seedStr, _states[seedStr].SerializeToString()));
            }

            return result;
        }

        public void SaveState()
        {
            var sb = new StringBuilder();
            foreach (var seed in _states.Keys)
            {
                if (sb.Length > 0)
                {
                    sb.Append("$");
                }

                try
                {
                    var strVal = JsonUtility.ToJson(InvStateSerializable.FromDesiredInventoryState(_states[seed]));
                    sb.Append(seed).Append("__");
                    sb.Append(strVal);
                }
                catch (Exception e)
                {
                    Warn($"failed to store desired inventory state as json in config {e.Message}\n{e.StackTrace}");
                }
            }

            PluginConfig.crossSeedInvState.Value = sb.ToString();
        }

        public static void Reset()
        {
            IsInitialized = false;

            instance = null;
        }

        public static void Save()
        {
            if (instance != null)
            {
                instance.SaveState();
                Debug("Saved desired inventory states");
            }
        }
    }
}