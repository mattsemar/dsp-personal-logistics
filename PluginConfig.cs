using BepInEx.Configuration;

namespace NetworkManager
{
    public class PluginConfig
    {
        public static ConfigEntry<int> workItemsPerFrame;
        public static ConfigFile PluginConfigFile;


        public static void InitConfig(ConfigFile configFile)
        {
            PluginConfigFile = configFile;


            workItemsPerFrame = configFile.Bind("Performance", "WorkItemsPerFrame", 1,
                new ConfigDescription("Number of actions attempted per Frame. Default value is 1 (minimum since 0 would not do anything other than queue up work). " +
                                      "Larger values might make the job complete more quickly, but will also slow your system down noticeably",
                    new AcceptableValueRange<int>(1, 25), "configEditOnly"));

        }
    }
}