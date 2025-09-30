using BepInEx.Configuration;
using UnityEngine;
using Logger = Jotunn.Logger;

namespace ItemConduit.Config
{
	public static class VisualConfig
	{
		#region Visual Config

		public static ConfigEntry<bool> containerWireframe { get; private set; }

		public static ConfigEntry<bool> nodeWireframe { get; private set; }

		public static ConfigEntry<bool> snappointSphere { get; private set; }

		public static ConfigEntry<bool> transferVisualEffect { get; private set; }

		#endregion
		public static void Initialize(ConfigFile config)
		{
			#region get Visual config
			containerWireframe = config.Bind(
				"Visual",
				"containerWireframe",
				false,
				new ConfigDescription("Draw wireframe to container bounds")
				);

			nodeWireframe = config.Bind(
				"Visual",
				"nodeWireframe",
				false,
				new ConfigDescription("Draw wireframe to nodes collider")
				);

			snappointSphere = config.Bind(
				"Visual",
				"snappointSphere",
				false,
				new ConfigDescription("Draw sphere at snappoint of nodes")
				);

			transferVisualEffect = config.Bind(
				"Visual",
				"transferVisualEffect",
				false,
				new ConfigDescription("Toggle visual effects for item transfers")
				);
			#endregion
		}
		
	}
}
