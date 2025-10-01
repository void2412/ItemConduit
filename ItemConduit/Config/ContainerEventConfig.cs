using BepInEx.Configuration;

namespace ItemConduit.Config
{
	public static class ContainerEventConfig
	{
		public static ConfigEntry<float> containerDetectionRange { get; private set; }
		public static ConfigEntry<float> containerEventDelay { get; private set; }
		public static void Initialize(ConfigFile config)
		{
			containerDetectionRange = config.Bind(
				"Detection",
				"containerDetectionRange",
				3f,
				new ConfigDescription("detection range of extract/insert node when a container is placed.")
			);

			containerEventDelay = config.Bind(
				"Detection",
				"containerEventDelay",
				0.5f,
				new ConfigDescription("Delay before start to notify nodes to update container")
			);
		}
	}
}
