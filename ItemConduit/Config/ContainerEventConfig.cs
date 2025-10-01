using BepInEx.Configuration;

namespace ItemConduit.Config
{
	public static class ContainerEventConfig
	{
		public static ConfigEntry<float> containerDetectionRange { get; private set; }

		public static void Initialize(ConfigFile config)
		{
			containerDetectionRange = config.Bind(
				"Detection",
				"containerDetectionRange",
				1f,
				new ConfigDescription("detection range of extract/insert node when a container is placed.")
			);
		}
	}
}
