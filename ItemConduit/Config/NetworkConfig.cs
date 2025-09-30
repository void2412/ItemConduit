using BepInEx.Configuration;
using UnityEngine;
using Logger = Jotunn.Logger;

namespace ItemConduit.Config
{
	/// <summary>
	/// Configuration for network performance tuning
	/// Allows server admins to adjust performance parameters without recompiling
	/// </summary>
	public static class NetworkPerformanceConfig
	{
		#region Rebuild Config
		public static ConfigEntry<float> rebuildInterval { get; private set; }

		/// <summary>Maximum nodes to process per frame in connection detection</summary>
		public static ConfigEntry<int> nodeProcessPerFrame { get; private set; }

		/// <summary>Maximum nodes to process per frame in network creation</summary>
		public static ConfigEntry<int> networkProcessPerFrame { get; private set; }

		/// <summary>Maximum time in milliseconds per frame for rebuild operations</summary>
		public static ConfigEntry<float> processingTimePerFrame { get; private set; }

		public static ConfigEntry<float> connectionRange {  get; private set; }

		#endregion

		#region Transfer Config
		public static ConfigEntry<float> transferTick {  get; private set; }

		public static ConfigEntry<int> transferRate { get; private set; }
		#endregion



		/// <summary>
		/// Initialize configuration entries
		/// </summary>
		public static void Initialize(ConfigFile config)
		{

			#region get transfer config

			transferRate = config.Bind(
				"Transfer Settings",
				"TransferRate",
				5,
				new ConfigDescription("Number of items transfer per tick for base node")
			);

			transferTick = config.Bind(
				"Transfer Settings",
				"TransferTick",
				0.5f,
				new ConfigDescription(
					"Interval between transfer operation in seconds",
					new AcceptableValueRange<float>(0.1f, 10f)
				)
			);

			#endregion

			#region get rebuild config

			rebuildInterval = config.Bind(
				"Network Rebuild Settings",
				"RebuildInterval",
				0.5f,
				new ConfigDescription("Interval between rebuild operation ")
			);

			nodeProcessPerFrame = config.Bind(
				"Network Rebuild Settings",
				"nodeProcessPerFrame",
				3,
				new ConfigDescription("Maximum nodes to process per frame in connection detection")
			);

			networkProcessPerFrame = config.Bind(
				"Network Rebuild Settings",
				"networkProcessPerFrame",
				5,
				new ConfigDescription("Maximum nodes to process per fame in network creation")
			);

			processingTimePerFrame = config.Bind(
				"Network Rebuild Settings",
				"processingTimePerFrame",
				8f,
				new ConfigDescription("Maximum time in milliseconds per frame for rebuild operations")
			);

			connectionRange = config.Bind(
				"Network Rebuild Settings",
				"connectionRange",
				0.2f,
				new ConfigDescription("Range to validate connections.")
			);
			#endregion
		}

	}

}