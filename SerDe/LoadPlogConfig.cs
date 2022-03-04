using System;
using System.Collections.Generic;
using System.IO;
using crecheng.DSPModSave;
using HarmonyLib;
using PersonalLogistics.Util;

namespace PersonalLogistics.SerDe
{
    /// <summary>
    /// Hack to make it possible to load our settings from the save state of the actual plog plugin
    /// </summary>
    public static class LoadPlogConfig
    {
        private static Dictionary<string, IModCanSave> allModData = new();
        private static string _lastLoadedSaveName = "";
        private const string saveExt = ".moddsv";

        [HarmonyPostfix]
        [HarmonyPatch(typeof(GameSave), "LoadCurrentGame")]
        private static void LoadCurrentGame(bool __result, string saveName)
        {
            if (!__result) return;

            if (DSPGame.Game == null)
            {
                Log.Warn("No game to load");
                return;
            }

            string path = GameConfig.gameSaveFolder + saveName + saveExt;
            if (!File.Exists(path))
            {
                return;
            }

            _lastLoadedSaveName = path;
        }

        private static void LoadData(FileStream fileStream)
        {
            using BinaryReader binaryReader = new BinaryReader(fileStream);

            Dictionary<string, ModSaveData> data = new Dictionary<string, ModSaveData>();
            bool flag = binaryReader.ReadChar() == 'M';
            flag = flag && binaryReader.ReadChar() == 'O';
            flag = flag && binaryReader.ReadChar() == 'D';
            if (!flag)
            {
                Log.Warn("Error loading save file. Save file is corrupted.");
                return;
            }

            int dataVersion = binaryReader.ReadInt32();
            binaryReader.ReadInt64();
            binaryReader.ReadInt64();
            binaryReader.ReadInt64();
            binaryReader.ReadInt64();
            binaryReader.ReadInt64();
            int count = binaryReader.ReadInt32();

            for (int i = 0; i < count; i++)
            {
                string name = binaryReader.ReadString();
                long begin = binaryReader.ReadInt64();
                long end = binaryReader.ReadInt64();
                data.Add(name, new ModSaveData(name, begin, end));
            }

            foreach (var d in allModData)
            {
                if (data.ContainsKey(d.Key))
                {
                    ModSaveData e = data[d.Key];
                    fileStream.Seek(e.begin, SeekOrigin.Begin);
                    byte[] b = new byte[e.end - e.begin];
                    fileStream.Read(b, 0, b.Length);
                    try
                    {
                        using MemoryStream temp = new MemoryStream(b);
                        using BinaryReader binary = new BinaryReader(temp);

                        d.Value.Import(binary);
                    }
                    catch (Exception ex)
                    {
                        Log.Warn(d.Key + " :mod data import error!");
                        Log.Warn(ex.Message + "\n" + ex.StackTrace);
                    }
                }
                else
                {
                    d.Value.IntoOtherSave();
                }
            }
        }

        public static bool LoadConfigFromActualPlugin()
        {
            if (_lastLoadedSaveName == "")
            {
                return false;
            }

            try
            {
                bool addedSelf = false;
                foreach (var d in BepInEx.Bootstrap.Chainloader.PluginInfos)
                {
                    if (d.Value.Instance is IModCanSave save)
                    {
                        if (d.Value.Metadata.GUID == "semarware.dysonsphereprogram.PersonalLogisticsFree")
                        {
                            allModData["semarware.dysonsphereprogram.PersonalLogistics"] = save;
                            addedSelf = true;
                            break;
                        }
                    }
                }

                if (addedSelf)
                {
                    try
                    {
                        using FileStream fileStream = new FileStream(_lastLoadedSaveName, FileMode.Open, FileAccess.Read);

                        LoadData(fileStream);
                        Log.Debug($"loaded settings from plog");
                        _lastLoadedSaveName = "";
                        return true;
                    }
                    catch (Exception exception)
                    {
                        Log.Warn("failed to load data. Oh well");
                        return false;
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn($"unsure, bailing");
            }

            return false;
        }
    }
}