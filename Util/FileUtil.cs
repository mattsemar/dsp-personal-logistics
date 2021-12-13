using System;
using System.IO;
using System.Reflection;

namespace PersonalLogistics.Util
{
    public class FileUtil
    {
        public static string GetBundleFilePath(string bundleFn = "pui")
        {
            var pluginFolderName = GetPluginFolderName();

            var fullAssetPath = Path.Combine(pluginFolderName, bundleFn);
            if (!File.Exists(fullAssetPath))
            {
                throw new NullReferenceException($"asset bundle not found at path {fullAssetPath}");
            }

            return fullAssetPath;
        }

        public static string GetPluginFolderName()
        {
            string assemblyLocation = null;
            try
            {
                assemblyLocation = Assembly.GetExecutingAssembly().Location;
            }
            catch (Exception e)
            {
                // ignored
            }

            if (string.IsNullOrEmpty(assemblyLocation))
            {
                assemblyLocation = "C:\\Program Files (x86)\\Steam\\steamapps\\common\\Dyson Sphere Program\\BepInEx\\plugins\\PersonalLogisticsPlugin.dll";
            }

            var pluginFolderName = Path.GetDirectoryName(assemblyLocation);
            if (string.IsNullOrEmpty(pluginFolderName))
            {
                pluginFolderName = "C:\\Program Files (x86)\\Steam\\steamapps\\common\\Dyson Sphere Program\\BepInEx\\plugins";
            }

            return pluginFolderName;
        }
    }
}