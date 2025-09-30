using BepInEx.Configuration;
using BepInEx.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Logger = Jotunn.Logger;

namespace ItemConduit.Config
{
	public static class ConfigManager
	{
		private static bool isInitialized = false;

		public static void Initialize(ConfigFile config)
		{
			if (isInitialized)
			{
				Logger.LogWarning("ConfigManager already intialized");
			}

			try
			{
				Logger.LogInfo("Initiliazing config");

				// Init each module
				NetworkPerformanceConfig.Initialize(config);
			}
			catch (Exception ex)
			{
				Logger.LogError($"Failed to initialize configuration: {ex.Message}");
				throw;
			}
		}

		public static void ReloadConfig(ConfigFile config)
		{
			Logger.LogInfo("Reloading configuration...");
			config.Reload();
		}

		public static void SaveConfig(ConfigFile config)
		{
			Logger.LogInfo("Saving configuration...");
			config.Save();
		}
	}
}
