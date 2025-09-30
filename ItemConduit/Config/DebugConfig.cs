using BepInEx.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ItemConduit.Config
{
	public static class DebugConfig
	{
		public static ConfigEntry<bool> showDebug {  get; private set; }

		public static void Initialize(ConfigFile config)
		{
			showDebug = config.Bind(
				"Debug",
				"showDebug",
				false,
				new ConfigDescription("Toggle debug message")
				);

		}
	}
}
