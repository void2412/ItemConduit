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
		// Configuration entries
		public static ConfigEntry<float> MaxMillisecondsPerFrame { get; private set; }
		public static ConfigEntry<int> NodesPerYield { get; private set; }
		public static ConfigEntry<float> OperationDelay { get; private set; }
		public static ConfigEntry<bool> EnablePerformanceLogging { get; private set; }
		public static ConfigEntry<string> PerformancePreset { get; private set; }

		/// <summary>
		/// Initialize configuration entries
		/// </summary>
		public static void Initialize(ConfigFile config)
		{
			// Performance preset selector
			PerformancePreset = config.Bind(
				"Network Performance",
				"Performance Preset",
				"Balanced",
				new ConfigDescription(
					"Performance preset to use. Options: Balanced, HighFPS, FastUpdates, Custom",
					new AcceptableValueList<string>("Balanced", "HighFPS", "FastUpdates", "Custom")
				)
			);

			// Individual tuning parameters (used when preset is "Custom")
			MaxMillisecondsPerFrame = config.Bind(
				"Network Performance",
				"Max Milliseconds Per Frame",
				5f,
				new ConfigDescription(
					"Maximum milliseconds to spend on network operations per frame. Lower = smoother FPS, higher = faster updates.",
					new AcceptableValueRange<float>(1f, 20f)
				)
			);

			NodesPerYield = config.Bind(
				"Network Performance",
				"Nodes Per Yield",
				10,
				new ConfigDescription(
					"Number of nodes to process before yielding control. Lower = smoother FPS, higher = faster processing.",
					new AcceptableValueRange<int>(1, 50)
				)
			);

			OperationDelay = config.Bind(
				"Network Performance",
				"Operation Delay",
				0.1f,
				new ConfigDescription(
					"Delay in seconds between processing queued operations. Lower = faster processing, higher = smoother FPS.",
					new AcceptableValueRange<float>(0.01f, 1f)
				)
			);

			EnablePerformanceLogging = config.Bind(
				"Network Performance",
				"Enable Performance Logging",
				false,
				"Log detailed performance metrics for network operations. Useful for tuning."
			);

			// Apply preset on change
			PerformancePreset.SettingChanged += (sender, args) => ApplyPreset();

			// Apply initial preset
			ApplyPreset();
		}

		/// <summary>
		/// Apply performance preset settings
		/// </summary>
		private static void ApplyPreset()
		{
			switch (PerformancePreset.Value)
			{
				case "Balanced":
					ApplyBalancedPreset();
					break;
				case "HighFPS":
					ApplyHighFPSPreset();
					break;
				case "FastUpdates":
					ApplyFastUpdatesPreset();
					break;
				case "Custom":
					// Use custom values as configured
					if (EnablePerformanceLogging.Value)
					{
						Logger.LogInfo($"[ItemConduit] Using custom performance settings: " +
							$"MaxMS={MaxMillisecondsPerFrame.Value}, " +
							$"NodesPerYield={NodesPerYield.Value}, " +
							$"OpDelay={OperationDelay.Value}");
					}
					break;
			}
		}

		/// <summary>
		/// Balanced preset - good for most situations
		/// </summary>
		private static void ApplyBalancedPreset()
		{
			MaxMillisecondsPerFrame.Value = 5f;
			NodesPerYield.Value = 10;
			OperationDelay.Value = 0.1f;

			if (EnablePerformanceLogging.Value)
			{
				Logger.LogInfo("[ItemConduit] Applied Balanced performance preset");
			}
		}

		/// <summary>
		/// High FPS preset - prioritizes smooth gameplay
		/// </summary>
		private static void ApplyHighFPSPreset()
		{
			MaxMillisecondsPerFrame.Value = 3f;
			NodesPerYield.Value = 5;
			OperationDelay.Value = 0.15f;

			if (EnablePerformanceLogging.Value)
			{
				Logger.LogInfo("[ItemConduit] Applied High FPS performance preset");
			}
		}

		/// <summary>
		/// Fast Updates preset - prioritizes quick network updates
		/// </summary>
		private static void ApplyFastUpdatesPreset()
		{
			MaxMillisecondsPerFrame.Value = 8f;
			NodesPerYield.Value = 20;
			OperationDelay.Value = 0.05f;

			if (EnablePerformanceLogging.Value)
			{
				Logger.LogInfo("[ItemConduit] Applied Fast Updates performance preset");
			}
		}

		/// <summary>
		/// Get current performance metrics for display
		/// </summary>
		public static string GetPerformanceMetrics()
		{
			return $"Preset: {PerformancePreset.Value}\n" +
				   $"Max MS/Frame: {MaxMillisecondsPerFrame.Value:F1}\n" +
				   $"Nodes/Yield: {NodesPerYield.Value}\n" +
				   $"Op Delay: {OperationDelay.Value:F2}s";
		}

		/// <summary>
		/// Log performance timing information
		/// </summary>
		public static void LogPerformanceTiming(string operation, float timeMs, int nodeCount)
		{
			if (EnablePerformanceLogging.Value)
			{
				Logger.LogInfo($"[ItemConduit Performance] {operation}: {timeMs:F2}ms for {nodeCount} nodes ({timeMs / nodeCount:F3}ms per node)");
			}
		}
	}

	/// <summary>
	/// Helper class for tracking performance metrics
	/// </summary>
	public class PerformanceTracker
	{
		private float startTime;
		private string operationName;
		private int nodeCount;

		public void StartTracking(string operation)
		{
			operationName = operation;
			startTime = Time.realtimeSinceStartup;
			nodeCount = 0;
		}

		public void IncrementNodeCount(int count = 1)
		{
			nodeCount += count;
		}

		public void EndTracking()
		{
			float elapsedMs = (Time.realtimeSinceStartup - startTime) * 1000f;
			NetworkPerformanceConfig.LogPerformanceTiming(operationName, elapsedMs, nodeCount);
		}

		public float GetElapsedMilliseconds()
		{
			return (Time.realtimeSinceStartup - startTime) * 1000f;
		}
	}
}